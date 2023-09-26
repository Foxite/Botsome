using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

	public ConfigItemsService(IOptionsMonitor<List<BotsomeItem>> options, ILogger<ConfigItemsService> logger) {
		m_Options = options;
		m_Logger = logger;

		m_OptionsChangeMonitor = options.OnChange(items => {
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
	
	private static bool AllowChannel(BotsomeItem item, DiscordChannel channel) {
		return (item.Trigger.OnlyInChannels is not { Count: > 0 } || item.Trigger.OnlyInChannels.Contains(channel.Id))
			&& (!channel.GuildId.HasValue || item.Trigger.OnlyInServers is not { Count: > 0 } || item.Trigger.OnlyInServers.Contains(channel.GuildId.Value));
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
