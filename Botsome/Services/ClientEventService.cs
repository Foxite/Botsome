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
			Console.WriteLine($"{m_RandomResponseTime.TotalMilliseconds}, {(latestReport - earliestReport).TotalMilliseconds}");
		};
	}

	public void OnMessageCreated(BotsomeClient client, MessageCreateEventArgs eventArgs) {
		m_Reports.Add(new ReportedEvent(client, eventArgs));
	}

	private void ProcessReports() {
		void AddReporterOrRespond(EventIdentifier eventIdentifier, TrackedEvent trackedEvent, ReportedEvent reportedEvent) {
			trackedEvent.ReportedAt.Add(DateTime.UtcNow);
			
			// If a client doesn't have the message content, it gets put into the list, and it responds when another client provides the message content and the item can be found.
			if (!trackedEvent.BotsomeItem.HasValue && reportedEvent.EventArgs.Message.Content != null) {
				trackedEvent.BotsomeItem = m_ItemsService.GetItem(reportedEvent.EventArgs);
			}

			if (trackedEvent.BotsomeItem.HasValue && trackedEvent.BotsomeItem.Value == null) {
				trackedEvent.StopTimer();
				return;
			}

			Debug.Assert(trackedEvent.BotsomeItem == null || trackedEvent.BotsomeItem.HasValue);

			if (trackedEvent.BotsomeItem == null || trackedEvent.BotsomeItem.Value!.RespondMode == BotSelection.Random) {
				trackedEvent.Reporters.Add(reportedEvent.Client);
			} else if (trackedEvent.BotsomeItem.Value.RespondMode == BotSelection.All) {
				trackedEvent.StopTimer();

				void RespondLocal(BotsomeClient client) {
					if (client.CanRespond(trackedEvent.BotsomeItem.Value) && client.Groups.Contains(trackedEvent.BotsomeItem.Value.RespondGroup)) {
						Respond(client, eventIdentifier, trackedEvent);
					}
				}
				
				RespondLocal(reportedEvent.Client);
				foreach (BotsomeClient client in trackedEvent.Reporters) {
					RespondLocal(client);
				}
				trackedEvent.Reporters.Clear();
			}
		}
		
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
					Console.WriteLine("Open");
					var ret = new TrackedEvent(this, m_RandomResponseTime, identifier);
					AddReporterOrRespond(identifier, ret, reportedEvent);
					return ret;
				},
				(identifier, evt) => AddReporterOrRespond(identifier, evt, reportedEvent)
			);
		}
	}

	private void Respond(BotsomeClient client, EventIdentifier eventIdentifier, TrackedEvent trackedEvent) {
		Console.WriteLine("Respond");
		Task.Run(async () => {
			try {
				await client.RespondAsync(eventIdentifier, trackedEvent.BotsomeItem.Value!);
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
		
		// Optional: has a value if ItemsService.GetItem has been called.
		// Value: the return value of GetItem
		public Optional<BotsomeItem?> BotsomeItem { get; set; } = Optional<BotsomeItem?>.Default;
		public List<BotsomeClient> Reporters { get; } = new();
		public List<DateTime> ReportedAt { get; } = new();

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
			Console.WriteLine($"Close {Reporters.Count}");
			m_RespondTimer.Dispose();
			// BotSelection.All will be handled upon reception
			if (BotsomeItem.HasValue && BotsomeItem.Value != null && BotsomeItem.Value.RespondMode == BotSelection.Random) {
				List<BotsomeClient> eligibleReporters = Reporters.Where(reporter => reporter.CanRespond(BotsomeItem.Value!)).ToList();

				if (eligibleReporters.Count == 0) {
					Console.WriteLine("No eligible responders");
					return;
				}
				
				int index = m_ClientEventService.m_Random.Next(0, eligibleReporters.Count);
				BotsomeClient selectedClient = eligibleReporters[index];
				m_ClientEventService.Respond(selectedClient, m_EventIdentifier, this);
			}
		}

		public void StopTimer() {
			if (m_RespondTimer.Enabled) {
				m_RespondTimer.Stop();
				m_RespondTimer.Dispose();
			}
		}
	}
}

public record EventIdentifier(
	ulong ChannelId,
	ulong MessageId
);
