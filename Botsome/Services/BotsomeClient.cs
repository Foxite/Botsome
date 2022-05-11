using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;

namespace Botsome; 

public class BotsomeClient : IAsyncDisposable {
	private static readonly Regex EmoteRegex = new Regex(@"^<a?:\w+:(?<id>\d{18})>$");
	
	private readonly DiscordClient m_Discord;
	private readonly BotsomeOptions m_Options;

	public string Token { get; }

	private BotsomeClient(string token, DiscordClient discord, BotsomeOptions options, string id, ResponseService responseService) {
		m_Discord = discord;
		m_Options = options;
		Token = token;

		discord.MessageCreated += (o, e) => {
			Match match = EmoteRegex.Match(e.Message.Content);
			if (match.Success && match.Groups["id"].Value == m_Options.EmoteId.ToString()) {
				responseService.OnBotsome(new BotsomeEvent(e.Channel.Id, e.Message.Id), id);
			}
			
			return Task.CompletedTask;
		};
	}

	public static async Task<BotsomeClient> CreateAsync(string token, string id, IServiceProvider isp) {
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = token,
			Intents = DiscordIntents.GuildMessages,
			LoggerFactory = isp.GetRequiredService<ILoggerFactory>()
		});

		var options = isp.GetRequiredService<IOptions<BotsomeOptions>>().Value;
		var responseService = isp.GetRequiredService<ResponseService>();

		await discord.ConnectAsync();

		return new BotsomeClient(token, discord, options, id, responseService);
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
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
	}
}