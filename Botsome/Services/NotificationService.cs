using DiscordWebhook;
using Microsoft.Extensions.Options;

namespace Botsome; 

public class NotificationService {
	private readonly Webhook m_Webhook;
	
	public NotificationService(IOptions<Config> config) {
		m_Webhook = new Webhook(config.Value.Url);
	}

	public void SendNotification(string message) {
		m_Webhook.PostData(new WebhookObject() {
			content = message,
		});
	}

	public class Config {
		public string Url { get; set; }
	}
}
