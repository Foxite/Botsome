using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;

namespace Botsome; 

/// <summary>
/// Provides BotsomeItems.
/// </summary>
public abstract class ItemsService {
	public abstract BotsomeItem? GetItem(MessageCreateEventArgs eventArgs, out ulong? emoteId);
}

public class ConfigItemsService : ItemsService {
	private static readonly Regex EmoteRegex = new Regex(@"<(?<animated>a?):(?<name>\w+):(?<id>\d{18})>");

	private readonly IOptionsMonitor<List<BotsomeItem>> m_Options;

	public ConfigItemsService(IOptionsMonitor<List<BotsomeItem>> options) {
		m_Options = options;
	}
	
	public override BotsomeItem? GetItem(MessageCreateEventArgs eventArgs, out ulong? emoteId) {
		foreach (BotsomeItem item in m_Options.CurrentValue) {
			if (AllowChannel(item, eventArgs.Channel) && ItemIsMatch(item, eventArgs, out emoteId)) {
				return item;
			}
		}

		emoteId = null;

		return null;
	}
	
	private static bool AllowChannel(BotsomeItem item, DiscordChannel channel) {
		return item.Trigger.OnlyInChannels is not { Count: > 0 } || item.Trigger.OnlyInChannels.Contains(channel.Id);
	}

	private static bool ItemIsMatch(BotsomeItem item, MessageCreateEventArgs eventArgs, out ulong? emoteId) {
		switch (item.Trigger.Type) {
			case TriggerType.MessageContent:
				emoteId = null;
				return item.Trigger.ActualMessageRegex!.IsMatch(eventArgs.Message.Content);
			case TriggerType.EmoteNameAsMessage: {
				foreach (Match match in EmoteRegex.Matches(eventArgs.Message.Content)) {
					if (item.Trigger.ActualEmoteNameRegex!.IsMatch(match.Groups["name"].Value)) {
						emoteId = ulong.Parse(match.Groups["id"].Value);
						return true;
					}
				}

				emoteId = null;
				return false;
			}
			default:
				emoteId = null;
				return false;
		}
	}
}