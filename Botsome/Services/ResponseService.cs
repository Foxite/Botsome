using System.Collections.Concurrent;
using System.Timers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Botsome; 

public class ResponseService {
	private readonly ILogger<ResponseService> m_Logger;
	private readonly ClientService m_ClientService;
	private readonly Random m_Random;
	private readonly ConcurrentDictionary<BotsomeEvent, BotsomeReports> m_IncomingReports = new();
	
	public ResponseService(ILogger<ResponseService> logger, ClientService clientService, Random random) {
		m_Logger = logger;
		m_ClientService = clientService;
		m_Random = random;
	}

	public void OnEmote(BotsomeEvent evt, string id) {
		m_IncomingReports.GetOrAdd(evt, be => new BotsomeReports(this, be)).Guids.Add(id);
	}

	private void OnExpired(BotsomeReports reports) {
		m_IncomingReports.TryRemove(reports.Event, out _);
		string chosenId = reports.Guids[m_Random.Next(0, reports.Guids.Count)];
		if (m_ClientService.GetClient(chosenId, out BotsomeClient? client)) {
			client.Respond(reports.Event);
		} else {
			m_Logger.LogWarning("Client closed after being chosen to respond {guid} {event}", chosenId, reports.Event);
		}
	}

	private class BotsomeReports {
		private readonly ResponseService m_Service;
		private readonly Timer m_Timer;
		
		public BotsomeEvent Event { get; }
		public List<string> Guids { get; }

		public BotsomeReports(ResponseService service, BotsomeEvent evt) {
			Guids = new List<string>(10);
			Event = evt;
			m_Service = service;
			m_Timer = new Timer();
			m_Timer.Interval = 750; // TODO config item
			m_Timer.AutoReset = false;
			m_Timer.Elapsed += Elapsed;
			m_Timer.Start();
		}

		private void Elapsed(object? sender, ElapsedEventArgs e) {
			m_Timer.Elapsed -= Elapsed;
			m_Timer.Dispose();
			m_Service.OnExpired(this);
		}
	}
}
