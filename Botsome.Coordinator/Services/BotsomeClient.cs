using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared;

namespace Botsome.Coordinator; 

public class BotsomeClient {
	private static readonly Regex EmoteRegex = new Regex(@"^<a?:\w+:(?<id>\d{18})>$");
	
	private readonly DiscordClient m_Discord;
	private readonly BotsomeOptions m_Options;

	private BotsomeClient(DiscordClient discord, BotsomeOptions options, Guid guid, ResponseService responseService) {
		m_Discord = discord;
		m_Options = options;

		discord.MessageCreated += (o, e) => {
			Match match = EmoteRegex.Match(e.Message.Content);
			if (match.Success && match.Groups["id"].Value == m_Options.EmoteId.ToString()) {
				responseService.OnBotsome(new BotsomeEvent(e.Channel.Id, e.Message.Id), guid);
			}
			
			return Task.CompletedTask;
		};
	}
	
	public static async Task<BotsomeClient> CreateAsync(string botToken, Guid id, IServiceProvider isp) {
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = botToken,
			Intents = DiscordIntents.GuildMessages
		});

		var options = isp.GetRequiredService<IOptions<BotsomeOptions>>().Value;
		var responseService = isp.GetRequiredService<ResponseService>();

		await discord.ConnectAsync();

		return new BotsomeClient(discord, options, id, responseService);
	}
	
	public void Respond(BotsomeEvent evt) {
		Task.Run(async () => {
			DiscordEmoji? discordEmoji;
			if (!DiscordEmoji.TryFromGuildEmote(m_Discord, m_Options.EmoteId, out discordEmoji)) {
				foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
					IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await kvp.Value.GetEmojisAsync();
					discordEmoji = guildEmojis.FirstOrDefault(emoji => emoji.Id == m_Options.EmoteId);
					if (discordEmoji != null) {
						break;
					}
				}
			}

			if (discordEmoji == null) {
				Console.WriteLine("Did not find emote");
				return;
			}

			DiscordChannel channel = await m_Discord.GetChannelAsync(evt.ChannelId);
			DiscordMessage message = await channel.GetMessageAsync(evt.MessageId);
			await message.CreateReactionAsync(discordEmoji);
		});
	}
}

public class BotsomeOptions {
	public ulong EmoteId { get; set; }
}
