using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Botsome; 

/// <summary>
/// Responsible for starting and stopping <see cref="BotsomeClient"/>s, and keeping track of the ones that are active.
/// </summary>
public class ClientService {
	private readonly ILogger<ClientService> m_Logger;
	private readonly ConcurrentDictionary<string, BotsomeClient> m_Clients = new();
	private readonly IServiceProvider m_ServiceProvider;

	public ClientService(ILogger<ClientService> logger, IServiceProvider serviceProvider) {
		m_ServiceProvider = serviceProvider;
		m_Logger = logger;
	}

	public async Task StartBotAsync(Bot bot) {
		try {
			var client = await BotsomeClient.CreateAsync(bot, m_ServiceProvider);
			if (!m_Clients.TryAdd(bot.Id, client)) {
				await client.DisposeAsync();
				m_Logger.LogCritical("Tried to add a bot with an ID that was already present: ID {Id}", bot.Id);
			}
		} catch (Exception e) {
			m_Logger.LogCritical(e, "Caught exception while creating bot with ID {Id}", bot.Id);
		}
	}

	public async Task StopBotAsync(string id) {
		try {
			if (m_Clients.TryRemove(id, out BotsomeClient? client)) {
				await client.DisposeAsync();
			}
		} catch (Exception e) {
			m_Logger.LogCritical(e, "Caught exception while removing bot with ID {Id}", id);
		}
	}
}
