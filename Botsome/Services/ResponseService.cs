using System.Collections.Concurrent;
using System.Timers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Botsome; 

public class ResponseService {
	private readonly ILogger<ResponseService> m_Logger;
	private readonly Random m_Random;
	private readonly ConcurrentDictionary<BotsomeEvent, BotsomeReports> m_IncomingReports = new();
	private readonly object m_ReportLock = new();
	
	public ResponseService(ILogger<ResponseService> logger, Random random) {
		m_Logger = logger;
		m_Random = random;
	}

	public async Task ReportAsync(BotsomeEvent evt, BotsomeClient client) {
		if (evt.Item.RespondUsing == BotSelection.All) {
			await client.RespondAsync(evt);
		} else { // BotSelection.Random
			lock (m_ReportLock) {
				BotsomeReports report = m_IncomingReports.GetOrAdd(evt, be => new BotsomeReports(this, be));
				report.Clients.Add(client);
			}
		}
	}

	private async Task OnExpiredAsync(BotsomeReports reports) {
		lock (m_ReportLock) {
			m_IncomingReports.TryRemove(reports.Event, out _);
		}

		BotsomeClient client = reports.Clients[m_Random.Next(0, reports.Clients.Count)];
		await client.RespondAsync(reports.Event);
	}

	private class BotsomeReports {
		private readonly ResponseService m_Service;
		private readonly Timer m_Timer;
		
		public BotsomeEvent Event { get; }
		public List<BotsomeClient> Clients { get; }

		public BotsomeReports(ResponseService service, BotsomeEvent evt) {
			Clients = new List<BotsomeClient>(10);
			Event = evt;
			m_Service = service;
			m_Timer = new Timer();
			m_Timer.Interval = 750; // TODO config item
			m_Timer.AutoReset = false;
			m_Timer.Elapsed += Elapsed;
			m_Timer.Start();
		}

		private void Elapsed(object? sender, ElapsedEventArgs ea) {
			m_Timer.Elapsed -= Elapsed;
			m_Timer.Dispose();
			try {
				m_Service.OnExpiredAsync(this).GetAwaiter().GetResult();
			} catch (Exception ex) {
				m_Service.m_Logger.LogError(ex, "Exception caught while responding from timer");
			}
		}
	}
}
