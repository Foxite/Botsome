using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Timers;
using Newtonsoft.Json;
using Shared;
using Timer = System.Timers.Timer;

namespace Botsome.Coordinator; 

public class BotsomeService {
	private readonly ILogger<BotsomeService> m_Logger;
	private readonly Random m_Random;
	private readonly Timer m_HeartbeatTimer;
	// TODO Not sure if these pipes ever actually get disposed in every scenario
	private readonly ConcurrentDictionary<Guid, Pipe> m_OutgoingStreams = new();
	private readonly ConcurrentDictionary<BotsomeEvent, BotsomeReports> m_IncomingReports = new();
	
	public BotsomeService(ILogger<BotsomeService> logger, Random random) {
		m_Logger = logger;
		m_Random = random;
		m_HeartbeatTimer = new Timer();
		m_HeartbeatTimer.Interval = 10_000;
		m_HeartbeatTimer.AutoReset = true;
		m_HeartbeatTimer.Elapsed += (o, e) => {
			foreach (var pipe in m_OutgoingStreams) {
				using var sw = new StreamWriter(pipe.Value.Writer.AsStream(), leaveOpen: true);
				sw.WriteLine();
				sw.Flush();
			}
		};
		m_HeartbeatTimer.Start();
	}

	public Stream OpenStream(Guid id) {
		var pipe = new Pipe();
		m_OutgoingStreams[id] = pipe;
		var stream = new DisposalNotifyingStream<Guid>(pipe.Reader.AsStream(), id);
		stream.Disposing += (sender, guid) => m_OutgoingStreams.TryRemove(guid, out _);
		return stream;
	}

	public void OnBotsome(BotsomeEvent evt, Guid id) {
		m_IncomingReports.GetOrAdd(evt, be => new BotsomeReports(this, be)).Guids.Add(id);
	}

	private void OnExpired(BotsomeReports reports) {
		m_IncomingReports.TryRemove(reports.Event, out _);
		Guid chosenId = reports.Guids[m_Random.Next(0, reports.Guids.Count)];
		if (m_OutgoingStreams.TryGetValue(chosenId, out Pipe? pipe)) {
			using (var sw = new StreamWriter(pipe.Writer.AsStream(), leaveOpen: true)) {
				sw.WriteLine(JsonConvert.SerializeObject(reports.Event, Formatting.None));
				sw.Flush();
			}

			pipe.Writer.AsStream().Flush();
		} else {
			m_Logger.LogCritical("Client is reporting but not listening {guid} {event}", chosenId, reports.Event);
		}
	}

	private class BotsomeReports {
		private readonly BotsomeService m_Service;
		private readonly Timer m_Timer;
		
		public BotsomeEvent Event { get; }
		public List<Guid> Guids { get; }

		public BotsomeReports(BotsomeService service, BotsomeEvent evt) {
			Guids = new List<Guid>(10);
			Event = evt;
			m_Service = service;
			m_Timer = new Timer();
			m_Timer.Interval = 3_000;
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
