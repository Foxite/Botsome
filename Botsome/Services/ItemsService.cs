using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;

namespace Botsome; 

/// <summary>
/// Provides BotsomeItems.
/// </summary>
public abstract class ItemsService {
	public abstract BotsomeItem? GetItem(DiscordEventArgs eventArgs, out ulong? emoteId);
}

public class ConfigItemsService : ItemsService {
	private static readonly Regex EmoteRegex = new Regex(@"<(?<animated>a?):(?<name>\w+):(?<id>\d+)>");

	private readonly IOptionsMonitor<List<BotsomeItem>> m_Options;

	public ConfigItemsService(IOptionsMonitor<List<BotsomeItem>> options) {
		m_Options = options;
	}
	
	public override BotsomeItem? GetItem(DiscordEventArgs eventArgs, out ulong? emoteId) {
		foreach (BotsomeItem item in m_Options.CurrentValue) {
			if (ItemIsMatch(item, eventArgs, out emoteId)) {
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

	private static bool ItemIsMatch(BotsomeItem item, DiscordEventArgs eventArgs, out ulong? emoteId) {
		if (eventArgs is MessageCreateEventArgs mcea) {
			if (!AllowChannel(item, mcea.Channel)) {
				emoteId = null;
				return false;
			}
			
			switch (item.Trigger.Type) {
				case TriggerType.MessageContent:
					emoteId = null;
					return item.Trigger.ActualMessageRegex!.IsMatch(mcea.Message.Content);
				case TriggerType.EmoteNameAsMessage:
				{
					foreach (Match match in EmoteRegex.Matches(mcea.Message.Content)) {
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
					return mcea.Author.Id == item.Trigger.UserId;
				default:
					emoteId = null;
					return false;
			}
		} else if (eventArgs is GuildBanAddEventArgs gbaea) {
			emoteId = null;
			if (item.Trigger.Type == TriggerType.UserBanned) {
				return item.Trigger.UserId == gbaea.Member.Id;
			}
		}
		
		emoteId = null;
		return false;
	}
}