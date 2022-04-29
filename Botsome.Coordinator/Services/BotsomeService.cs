using System.Collections.Concurrent;
using Shared;

namespace Botsome.Coordinator; 

public class BotsomeService {
	public event EventHandler<BotsomeEvent> Botsome;

	public IAsyncEnumerable<BotsomeEvent> OpenStream() {
		return new AsyncEnumeratorWrapper<BotsomeEvent>(ct => new BotsomeStreamContext(this, ct));
	}

	public void OnBotsome(BotsomeEvent evt) {
		Botsome?.Invoke(this, evt);
	}

	public void SubscribeContext(BotsomeStreamContext ctx) {
		Botsome += ctx.OnBotsome;
	}
	
	public void UnsubscribeContext(BotsomeStreamContext ctx) {
		Botsome += ctx.OnBotsome;
	}

	public class BotsomeStreamContext : IAsyncEnumerator<BotsomeEvent> {
		private readonly object m_Monitor = new();
		private readonly BotsomeService m_Service;
		private readonly CancellationToken m_CancellationToken;
		private readonly ConcurrentQueue<BotsomeEvent> m_Queue = new();
		private bool m_Disposed = false;

		public BotsomeEvent Current { get; private set; } = null!;

		public BotsomeStreamContext(BotsomeService service, CancellationToken cancellationToken) {
			m_Service = service;
			m_CancellationToken = cancellationToken;
		}

		public void OnBotsome(object? sender, BotsomeEvent evt) {
			m_Queue.Enqueue(evt);
			Monitor.Pulse(m_Monitor);
		}
	
		public async ValueTask<bool> MoveNextAsync() {
			lock (m_Monitor) {
				BotsomeEvent? evt;
				
				if (m_Disposed) {
					return false;
				}
				if (m_Queue.TryPeek(out evt)) {
					Current = evt;
					return true;
				} else {
					// TODO cancellationtoken
					// TODO make actually async
					Monitor.Wait(m_Monitor);
					
					if (m_Disposed) {
						return false;
					}
					if (m_Queue.TryPeek(out evt)) {
						Current = evt;
						return true;
					}
					throw new Exception("Should never happen");
				}
			}
		}
	
		public ValueTask DisposeAsync() {
			m_Service.UnsubscribeContext(this);
			lock (m_Monitor) {
				m_Disposed = true;
				Monitor.Pulse(m_Monitor);
			}
			return default;
		}
	}
}
