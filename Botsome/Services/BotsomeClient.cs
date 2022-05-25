using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;

namespace Botsome; 

public class BotsomeClient : IAsyncDisposable {
	private static readonly Regex EmoteRegex = new Regex(@"^<(?<animated>a?):(?<name>\w+):(?<id>\d{18})>$");
	
	private readonly DiscordClient m_Discord;
	private readonly BotsomeOptions m_Options;
	private DiscordGuildEmoji m_Emote;

	public string Token { get; }

	private BotsomeClient(string token, DiscordClient discord, BotsomeOptions options, string id, ResponseService responseService, ILogger<BotsomeClient> logger) {
		m_Discord = discord;
		m_Options = options;
		Token = token;

		discord.MessageCreated += (o, e) => {
			MatchCollection matches = EmoteRegex.Matches(e.Message.Content);
			if (matches.Any(match => match.Groups["name"].Value.ToLower() == m_Options.EmoteName)) {
				responseService.OnBotsome(new BotsomeEvent(e.Channel.Id, e.Message.Id), id);
			}
			
			return Task.CompletedTask;
		};

		discord.Ready += async (_, _) => {
			foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
				IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await kvp.Value.GetEmojisAsync();
				m_Emote = guildEmojis.FirstOrDefault(emoji => emoji.Name == m_Options.EmoteName);
				if (m_Emote != null) {
					break;
				}
			}

			if (m_Emote == null) {
				logger.LogCritical("Did not find emote, ID: {Id}", id);
				await m_Discord.DisconnectAsync();
			}
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
		var logger = isp.GetRequiredService<ILogger<BotsomeClient>>();

		await discord.ConnectAsync();

		return new BotsomeClient(token, discord, options, id, responseService, logger);
	}
	
	public void Respond(BotsomeEvent evt) {
		Task.Run(async () => {
			DiscordChannel channel = await m_Discord.GetChannelAsync(evt.ChannelId);
			DiscordMessage message = await channel.GetMessageAsync(evt.MessageId);
			await message.CreateReactionAsync(m_Emote);
		});
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
	}
}