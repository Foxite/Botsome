using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Botsome.Coordinator; 

public class ClientService {
	private readonly ConcurrentDictionary<Guid, BotsomeClient> m_Clients = new();

	public Guid AddClient(BotsomeClient client) {
		while (true) {
			var guid = Guid.NewGuid();
			if (m_Clients.TryAdd(guid, client)) {
				return guid;
			}
		}
	}

	public bool RemoveClient(Guid id) {
		return m_Clients.TryRemove(id, out _);
	}

	public bool GetClient(Guid id, [NotNullWhen(true)] out BotsomeClient? client) {
		return m_Clients.TryGetValue(id, out client);
	}
}
