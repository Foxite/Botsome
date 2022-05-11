using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Botsome.Coordinator; 

public class ClientService {
	private readonly ConcurrentDictionary<string, BotsomeClient> m_Clients = new();
	private readonly IServiceProvider m_ServiceProvider;

	public ClientService(IServiceProvider serviceProvider) {
		m_ServiceProvider = serviceProvider;
	}
	
	public async Task AddClientAsync(string id, string token) {
		var client = await BotsomeClient.CreateAsync(token, id, m_ServiceProvider);

		if (!m_Clients.TryAdd(id, client)) {
			client.Dispose();
			throw new InvalidOperationException("Already have a client with ID " + client);
		}
	}

	public bool RemoveClient(string id) {
		if (m_Clients.TryRemove(id, out BotsomeClient? client)) {
			client.Dispose();
			return true;
		} else {
			return false;
		}
	}

	public bool GetClient(string id, [NotNullWhen(true)] out BotsomeClient? client) {
		return m_Clients.TryGetValue(id, out client);
	}
}
