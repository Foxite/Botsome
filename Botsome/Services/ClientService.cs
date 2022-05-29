using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Botsome; 

public class ClientService {
	private readonly ILogger<ClientService> m_Logger;
	private readonly ConcurrentDictionary<string, BotsomeClient> m_Clients = new();
	private readonly IServiceProvider m_ServiceProvider;

	public ClientService(ILogger<ClientService> logger, IServiceProvider serviceProvider) {
		m_ServiceProvider = serviceProvider;
		m_Logger = logger;
	}

	public async Task UpdateList(IEnumerable<(string Id, string Token)> bots) {
		KeyValuePair<string, BotsomeClient>[] toRemove = m_Clients.Where(kvp => !bots.Select(bot => bot.Id).Contains(kvp.Key)).ToArray();
		(string Id, string Token)[] toAdd = bots.Select(bot => bot).Where(kvp => !m_Clients.ContainsKey(kvp.Id)).ToArray();

		foreach ((string? id, BotsomeClient? client) in toRemove) {
			try {
				if (!m_Clients.TryRemove(id, out _)) {
					m_Logger.LogWarning("Tried to remove bot that wasn't present: ID {id}, token ending in {tokenSuffix}", id, client.Token[^3..]);
				}

				await client.DisposeAsync();
			} catch (Exception e) {
				m_Logger.LogCritical(e, "Caught exception while removing bot with ID {id}", id);
			}
		}

		foreach ((string id, string token) in toAdd) {
			try {
				var client = await BotsomeClient.CreateAsync(token, id, m_ServiceProvider);
				if (!m_Clients.TryAdd(id, client)) {
					await client.DisposeAsync();
					m_Logger.LogWarning("Tried to add a bot with an ID that was already present: ID {id}, token ending in {tokenSuffix}", id, token[^3..]);
				}
			} catch (Exception e) {
				m_Logger.LogCritical(e, "Caught exception while creating bot with ID {id}", id);
			}
		}
	}
	
	public bool GetClient(string id, [NotNullWhen(true)] out BotsomeClient? client) {
		return m_Clients.TryGetValue(id, out client);
	}
}
