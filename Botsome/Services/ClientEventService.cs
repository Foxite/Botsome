using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using Botsome.Util;
using DSharpPlus.EventArgs;
using Emzi0767;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Botsome;

/// <summary>
/// Receives events from <see cref="BotsomeClient"/>s and orchestrates them to respond.
/// </summary>
public sealed class ClientEventService : IDisposable {
	private readonly IOptions<BotsomeOptions> m_Options;
	private readonly ILogger<ClientEventService> m_Logger;
	private readonly ItemsService m_ItemsService;
	private readonly Thread m_ProcessReportsThread;
	private readonly Random m_Random;
	
	private readonly CancellationTokenSource m_ProcessReportsCancellation = new();
	private readonly BlockingCollection<ReportedEvent> m_Reports = new();
	private readonly Queue<TimeSpan> m_LatestDelayWindows = new();
	private readonly ExpiringDictionary<EventIdentifier, TrackedEvent> m_TrackedEvents;

	private TimeSpan m_RandomResponseTime;

	public ClientEventService(IOptions<BotsomeOptions> options, ItemsService itemsService, ILogger<ClientEventService> logger, Random random) {
		m_Options = options;
		m_ItemsService = itemsService;
		m_Logger = logger;
		m_Random = random;
		m_RandomResponseTime = m_Options.Value.RandomResponseTime;

		m_ProcessReportsThread = new Thread(ProcessReports);
		m_ProcessReportsThread.Start();
		m_TrackedEvents = new ExpiringDictionary<EventIdentifier, TrackedEvent>(options.Value.RandomResponseTime);
		m_TrackedEvents.EntryExpired += (_, kvp) => {
			if (kvp.Value.ReportedAt.Count < 2 || options.Value.RandomResponseTimeMode == RandomDelayMode.Fixed) {
				return;
			}
			
			DateTime earliestReport = kvp.Value.ReportedAt.Min();
			DateTime latestReport = kvp.Value.ReportedAt.Max();
			m_LatestDelayWindows.Enqueue(latestReport - earliestReport);

			while (m_LatestDelayWindows.Count > 10) {
				m_LatestDelayWindows.Dequeue();
			}

			m_RandomResponseTime = TimeSpan.FromSeconds(Math.Min(m_LatestDelayWindows.Max(ts => ts.TotalSeconds) * 1.5, m_Options.Value.RandomResponseTimeSeconds));
			m_Logger.LogTrace("{RandomResponseTimeMillis}, {WindowMillis}", m_RandomResponseTime.TotalMilliseconds, (latestReport - earliestReport).TotalMilliseconds);
		};
	}

	public void OnMessageCreated(BotsomeClient client, MessageCreateEventArgs eventArgs) {
		m_Reports.Add(new ReportedEvent(client, eventArgs));
	}

	private void ProcessReports() {
		while (!m_ProcessReportsCancellation.IsCancellationRequested) {
			ReportedEvent reportedEvent;
			try {
				reportedEvent = m_Reports.Take(m_ProcessReportsCancellation.Token);
			} catch (OperationCanceledException) {
				return;
			}

			m_TrackedEvents.AddOrUpdate(
				reportedEvent.GetIdentifier(),
				identifier => {
					m_Logger.LogTrace("Open");
					var ret = new TrackedEvent(this, m_RandomResponseTime, identifier);
					ret.AddReporter(reportedEvent);
					return ret;
				},
				(_, evt) => evt.AddReporter(reportedEvent)
			);
		}
	}

	private void Respond(BotsomeClient client, EventIdentifier eventIdentifier, TrackedEvent trackedEvent) {
		m_Logger.LogTrace("Respond");
		Task.Run(async () => {
			try {
				await client.RespondAsync(eventIdentifier, trackedEvent.BotsomeItem.Value!, trackedEvent.EmoteId);
			} catch (Exception e) {
				// TODO log client id, event identifier, and item
				m_Logger.LogError(e, "Caught exception while responding to event");
			}
		});
	}

	public void Dispose() {
		m_ProcessReportsCancellation.Cancel();
		m_Reports.Dispose();
		m_ProcessReportsCancellation.Dispose();
	}

	private record ReportedEvent(
		BotsomeClient Client,
		MessageCreateEventArgs EventArgs
	) {
		public EventIdentifier GetIdentifier() => new(EventArgs.Channel.Id, EventArgs.Message.Id);
	};
	
	private class TrackedEvent {
		private readonly ClientEventService m_ClientEventService;
		private readonly EventIdentifier m_EventIdentifier;
		private readonly Timer m_RespondTimer;
		private readonly List<BotsomeClient> m_Reporters = new();

		// Optional: has a value if ItemsService.GetItem has been called.
		// Value: the return value of GetItem
		public Optional<BotsomeItem?> BotsomeItem { get; private set; } = Optional<BotsomeItem?>.Default;
		public List<DateTime> ReportedAt { get; } = new();
		public ulong? EmoteId { get; private set; }

		public TrackedEvent(ClientEventService clientEventService, TimeSpan respondAfter, EventIdentifier eventIdentifier) {
			m_ClientEventService = clientEventService;
			m_EventIdentifier = eventIdentifier;

			m_RespondTimer = new Timer();
			m_RespondTimer.Interval = respondAfter.TotalMilliseconds;
			m_RespondTimer.AutoReset = false;
			m_RespondTimer.Elapsed += Elapsed;
			m_RespondTimer.Start();
		}

		private void Elapsed(object? sender, ElapsedEventArgs e) {
			m_ClientEventService.m_Logger.LogTrace("Close {Count}", m_Reporters.Count);
			m_RespondTimer.Dispose();
			if (BotsomeItem.HasValue && BotsomeItem.Value != null) {
				List<BotsomeClient> eligibleResponders = m_Reporters.Where(reporter => reporter.CanRespond(BotsomeItem.Value!)).ToList();

				if (eligibleResponders.Count == 0) {
					m_ClientEventService.m_Logger.LogTrace("No eligible responders");
					return;
				}

				if (BotsomeItem.Value.RespondMode == BotSelection.Random) {
					int index = m_ClientEventService.m_Random.Next(0, eligibleResponders.Count);
					BotsomeClient selectedClient = eligibleResponders[index];
					m_ClientEventService.Respond(selectedClient, m_EventIdentifier, this);
				} else {
					foreach (BotsomeClient client in eligibleResponders) {
						m_ClientEventService.Respond(client, m_EventIdentifier, this);
					}
				}
			}
		}

		private void StopTimer() {
			if (m_RespondTimer.Enabled) {
				m_RespondTimer.Stop();
				m_RespondTimer.Dispose();
			}
		}

		public void AddReporter(ReportedEvent reportedEvent) {
			ReportedAt.Add(DateTime.UtcNow);
			
			if (!BotsomeItem.HasValue && reportedEvent.EventArgs.Message.Content != null) {
				BotsomeItem = m_ClientEventService.m_ItemsService.GetItem(reportedEvent.EventArgs, out ulong? emoteId);
				EmoteId ??= emoteId;
			}

			if (BotsomeItem.HasValue && BotsomeItem.Value == null) {
				StopTimer();
				return;
			}

			Debug.Assert(BotsomeItem == null || BotsomeItem.HasValue);

			m_Reporters.Add(reportedEvent.Client);
		}
	}
}

public record EventIdentifier(
	ulong ChannelId,
	ulong MessageId
);
