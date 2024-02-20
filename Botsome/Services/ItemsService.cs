using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Botsome; 

/// <summary>
/// Provides BotsomeItems.
/// </summary>
public abstract class ItemsService {
	public abstract BotsomeItem? GetItem(MessageCreateEventArgs eventArgs, out ulong? emoteId);
}

public class ConfigItemsService : ItemsService, IDisposable {
	private static readonly Regex EmoteRegex = new Regex(@"<(?<animated>a?):(?<name>\w+):(?<id>\d+)>");

	private readonly ILogger<ConfigItemsService> m_Logger;
	private readonly IOptionsMonitor<List<BotsomeItem>> m_Options;
	private readonly IDisposable m_OptionsChangeMonitor;
	private readonly NotificationService m_NotificationService;

	public ConfigItemsService(IOptionsMonitor<List<BotsomeItem>> options, ILogger<ConfigItemsService> logger, NotificationService notificationService) {
		m_Options = options;
		m_Logger = logger;
		m_NotificationService = notificationService;
		
		m_Logger.LogDebug("There are {Count} items", options.CurrentValue.Count);
		m_Logger.LogTrace("Items: {Config}", JsonConvert.SerializeObject(options.CurrentValue));

		m_OptionsChangeMonitor = options.OnChange(items => {
			m_Logger.LogDebug("There are now {Count} items", options.CurrentValue.Count);
			m_Logger.LogTrace("Items: {Config}", JsonConvert.SerializeObject(options.CurrentValue));
			for (int i = 0; i < items.Count; i++) {
				BotsomeItem? item = items[i];
				if (item.ResponseSelection == ResponseSelection.Random && item.RespondMode == BotSelection.RandomPerResponse) {
					m_Logger.LogError("Botsome item with index {Index} has ResponseSelection: Random and RespondMode: RandomPerResponse. This is not supported, ResponseSelection will work as All in this case", i);
				}
			}
		});
	}
	
	public override BotsomeItem? GetItem(MessageCreateEventArgs eventArgs, out ulong? emoteId) {
		foreach (BotsomeItem item in m_Options.CurrentValue) {
			if (item.Enabled && AllowChannel(item, eventArgs.Channel) && ItemIsMatch(item, eventArgs, out emoteId)) {
				return item;
			}
		}

		emoteId = null;

		return null;
	}
	
	private bool AllowChannel(BotsomeItem item, DiscordChannel channel) {
		// Channel allowlist is not empty, but the channel's ID is not in it
		if (item.Trigger.OnlyInChannels != null && item.Trigger.OnlyInChannels.Count > 0) {
			if (!item.Trigger.OnlyInChannels.Contains(channel.Id)) {
				return false;
			}
		}

		// Guild allowlist is not empty, but the channel is not a guild channel, or the guild's ID is not allowed.
		if (item.Trigger.OnlyInServers != null && item.Trigger.OnlyInServers.Count > 0) {
			if (channel.Type is ChannelType.Private or ChannelType.Group) {
				return false;
			}

			if (!channel.GuildId.HasValue) {
				m_NotificationService.SendNotification($"Channel.GuildId is missing for non-DM channel: {channel.Guild?.Id}/{channel.Id} // {channel.Guild?.Name} / {channel.Name}");

				return false;
			}

			if (!item.Trigger.OnlyInServers.Contains(channel.GuildId.Value)) {
				return false;
			}
		}

		return true;
	}

	private static bool ItemIsMatch(BotsomeItem item, MessageCreateEventArgs eventArgs, out ulong? emoteId) {
		switch (item.Trigger.Type) {
			case TriggerType.MessageContent:
				emoteId = null;
				return item.Trigger.ActualMessageRegex!.IsMatch(eventArgs.Message.Content);
			case TriggerType.EmoteNameAsMessage: {
				foreach (Match match in EmoteRegex.Matches(eventArgs.Message.Content)) {
					string emoteName = match.Groups["name"].Value;
					int tildeIndex = emoteName.IndexOf('~');
					if (tildeIndex != -1) {
						emoteName = emoteName[..(tildeIndex - 1)];
					}

					if (item.Trigger.ActualEmoteNameRegex!.IsMatch(emoteName)) {
						emoteId = ulong.Parse(match.Groups["id"].Value);
						return true;
					}
				}

				emoteId = null;
				return false;
			}
			case TriggerType.MessageFromUser:
				emoteId = null;
				return eventArgs.Author.Id == item.Trigger.UserId;
			default:
				emoteId = null;
				return false;
		}
	}
	
	public void Dispose() {
		m_OptionsChangeMonitor.Dispose();
	}
}
