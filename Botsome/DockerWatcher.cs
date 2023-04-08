using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Botsome; 

public class DockerWatcher : IHostedService {
	private readonly DockerClient m_Docker;
	private readonly ClientService m_ClientService;
	private readonly ILogger<DockerWatcher> m_Logger;
	private readonly CancellationTokenSource m_Cts = new CancellationTokenSource();
	private Task? m_MonitorTask = null;

	public DockerWatcher(DockerClient docker, ClientService clientService, ILogger<DockerWatcher> logger) {
		m_Docker = docker;
		m_ClientService = clientService;
		m_Logger = logger;
	}
	
	public async Task StartAsync(CancellationToken cancellationToken) {
		cancellationToken.Register(() => m_Cts.Cancel());
		
		IList<ContainerListResponse> containers = await m_Docker.Containers.ListContainersAsync(new ContainersListParameters() {
			Filters = new Dictionary<string, IDictionary<string, bool>>() {
				{ "label", new Dictionary<string, bool>() { { "me.foxite.botsome", true } } }
			}
		});

		foreach (ContainerListResponse container in containers) {
			await CheckContainerAndStartBot(container.ID);
		}

		m_MonitorTask = m_Docker.System.MonitorEventsAsync(new ContainerEventsParameters(), new Progress<Message>(HandleMessage), m_Cts.Token);
	}

	public async Task StopAsync(CancellationToken cancellationToken) {
		m_Cts.Cancel();

		if (m_MonitorTask != null) {
			try {
				await m_MonitorTask;
			} catch (TaskCanceledException) { }
		}
	}
	
	private async Task CheckContainerAndStartBot(string id) {
		ContainerInspectResponse container = await m_Docker.Containers.InspectContainerAsync(id);
		IDictionary<string, string> labels = container.Config.Labels;
		IList<string> envvars = container.Config.Env;
	
		if (!labels.ContainsKey("me.foxite.botsome")) {
			return;
		}

		string tokenEnvvar = labels.TryGetValue("me.foxite.botsome.envvar", out string? labelValue) ? labelValue : "BOT_TOKEN";
		string? token = envvars.FirstOrDefault(env => env.StartsWith(tokenEnvvar + "="));

		if (token == null) {
			m_Logger.LogWarning("Could not get the envvar {Envvar} of container {Container}", tokenEnvvar, id);
			return;
		}

		token = token[(token.IndexOf('=') + 1)..];
		string groups = labels.TryGetValue("me.foxite.botsome.groups", out string? groupsValue) ? (groupsValue + " all") : "all";

		await m_ClientService.StartBotAsync(new Bot(id, token, groups));
	}

	private void HandleMessage(Message message) {
		Task.Run(async () => {
			try {
				if (message.Type == "container") {
					if (message.Status == "die") {
						await m_ClientService.StopBotAsync(message.ID);
					} else if (message.Status == "start") {
						await CheckContainerAndStartBot(message.ID);
					}
				}
			} catch (Exception e) {
				m_Logger.LogError(e, "Update failed");
			}
		});
	}
}
