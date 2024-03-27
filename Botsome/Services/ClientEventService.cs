using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Timers;
using Botsome.Util;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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
		PropertyInfo roleIdsProperty = typeof(DiscordMember).GetProperty("RoleIds", BindingFlags.NonPublic | BindingFlags.Instance)!;
		var roleIds = (IReadOnlyList<ulong>) roleIdsProperty.GetValue(eventArgs.Author)!;
		//(eventArgs.Author as DiscordMember)?.Roles.Count(); // 0
		//roleIds.Count(); // 1, no, i dont know why.
		if (eventArgs.Author is DiscordMember authorMember && m_Options.Value.IgnoredRoles.Intersect(roleIds).Any()) {
			m_Logger.LogDebug("Not responding; ignored due to roles");
			return;
		}

		if (m_Options.Value.IgnoredChannels.Contains(eventArgs.Channel.Id)) {
			m_Logger.LogDebug("Not responding; ignored due to global channel blocklist");
			return;
		}

		if (eventArgs.Guild != null && m_Options.Value.IgnoredGuilds.Contains(eventArgs.Guild.Id)) {
			m_Logger.LogDebug("Not responding; ignored due to global guild blocklist");
			return;
		}
		
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

	private void Respond(BotsomeClient client, EventIdentifier eventIdentifier, TrackedEvent trackedEvent, IEnumerable<BotsomeResponse> responses) {
		m_Logger.LogTrace("Respond");

		if (m_Random.NextDouble() > trackedEvent.BotsomeItem.Value!.Trigger.Probability) {
			m_Logger.LogTrace("Probability miss");
			return;
		}
		
		Task.Run(async () => {
			try {
				await client.RespondAsync(eventIdentifier, responses, trackedEvent.EmoteId);
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
		public Emzi0767.Optional<BotsomeItem?> BotsomeItem { get; private set; } = Emzi0767.Optional<BotsomeItem?>.Default;
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
				List<BotsomeResponse> responses;
				if (BotsomeItem.Value.ResponseSelection == ResponseSelection.All) {
					responses = BotsomeItem.Value.Responses;
				} else if (BotsomeItem.Value.ResponseSelection == ResponseSelection.Random) {
					responses = new List<BotsomeResponse>() {
						BotsomeItem.Value.Responses[m_ClientEventService.m_Random.Next(0, BotsomeItem.Value.Responses.Count)]
					};
				} else {
					m_ClientEventService.m_Logger.LogCritical("Encountered invalid ResponseSelection {ResponseSelection}", BotsomeItem.Value.ResponseSelection);
					throw new Exception("What the fuck");
				}
				
				List<(BotsomeClient Client, ICollection<BotsomeResponse> Responses)> eligibleResponders = m_Reporters
					.Select(reporter => (Client: reporter, Responses: reporter.CanRespond(BotsomeItem.Value, responses)))
					.Where(tuple => tuple.Responses.Count > 0)
					.ToList();

				if (eligibleResponders.Count == 0) {
					m_ClientEventService.m_Logger.LogTrace("No eligible responders");
					return;
				}

				if (BotsomeItem.Value.RespondMode == BotSelection.All) {
					foreach ((BotsomeClient selectedClient, ICollection<BotsomeResponse> selectedResponses) in eligibleResponders) {
						m_ClientEventService.Respond(selectedClient, m_EventIdentifier, this, selectedResponses);
					}
				} else if (BotsomeItem.Value.RespondMode == BotSelection.Random) {
					int index = m_ClientEventService.m_Random.Next(0, eligibleResponders.Count);
					(BotsomeClient selectedClient, ICollection<BotsomeResponse> selectedResponses) = eligibleResponders[index];
					m_ClientEventService.Respond(selectedClient, m_EventIdentifier, this, selectedResponses);
				} else if (BotsomeItem.Value.RespondMode == BotSelection.RandomPerResponse) {
					// Note: if ResponseSelection is Random, it will be ignored; a warning will have been emitted by ConfigItemsService
					foreach (BotsomeResponse response in BotsomeItem.Value.Responses) {
						int index = m_ClientEventService.m_Random.Next(0, eligibleResponders.Count);
						(BotsomeClient selectedClient, ICollection<BotsomeResponse> selectedResponses) = eligibleResponders[index];
						m_ClientEventService.Respond(selectedClient, m_EventIdentifier, this, new[] { response });
					}
				}
			} else {
				m_ClientEventService.m_Logger.LogTrace("No match");
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
				m_ClientEventService.m_Logger.LogTrace("No match");
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
