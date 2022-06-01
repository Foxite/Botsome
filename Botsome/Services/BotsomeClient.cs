using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Botsome;

public class BotsomeClient : IAsyncDisposable {
	private static readonly Regex EmoteRegex = new Regex(@"<(?<animated>a?):(?<name>\w+):(?<id>\d{18})>");
	
	private readonly DiscordClient m_Discord;
	private readonly BotsomeOptions m_Options;
	private readonly Regex? m_WordRegex;
	private DiscordGuildEmoji m_Emote;

	public string Token { get; }

	// ReSharper disable warning CS8618
	private BotsomeClient(string token, DiscordClient discord, BotsomeOptions options, string id, ResponseService responseService, ILogger<BotsomeClient> logger) {
		m_Discord = discord;
		m_Options = options;
		Token = token;

		if (options.Word != null) {
			m_WordRegex = new Regex(@$"(^|\b){options.Word}($|\b)");
			
			discord.MessageCreated += (o, e) => {
				if (!e.Author.IsBot && (!m_Options.WordsOnlyInChannel.HasValue || m_Options.WordsOnlyInChannel.Value == e.Channel.Id) && m_WordRegex.IsMatch(e.Message.Content)) {
					return e.Channel.SendMessageAsync(m_Options.Word);
				}
				
				return Task.CompletedTask;
			};
		}

		if (m_Options.EmoteName != null) {
			discord.MessageCreated += (o, e) => {
				MatchCollection matches = EmoteRegex.Matches(e.Message.Content);
				if (matches.Any(match => match.Groups["name"].Value.ToLower() == m_Options.EmoteName)) {
					responseService.OnEmote(new BotsomeEvent(e.Channel.Id, e.Message.Id), id);
				}

				return Task.CompletedTask;
			};
		}

		discord.Ready += (_, _) => {
			Task.Run(async () => {
				foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
					IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await kvp.Value.GetEmojisAsync();
					// ReSharper disable warning CS8601
					m_Emote = guildEmojis.FirstOrDefault(emoji => emoji.Name == m_Options.EmoteName);
					if (m_Emote != null) {
						break;
					}
				}

				if (m_Emote == null) {
					logger.LogCritical("Did not find emote, ID: {Id}", id);
					await m_Discord.DisconnectAsync();
				}
			});
			return Task.CompletedTask;
		};
	}

	public static async Task<BotsomeClient> CreateAsync(string token, string id, IServiceProvider isp) {
		ILoggerFactory loggerFactory = isp.GetRequiredService<ILoggerFactory>().Scope("ID: {Id}", id);
		
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = token,
			Intents = DiscordIntents.GuildMessages,
			LoggerFactory = loggerFactory
		});

		var options = isp.GetRequiredService<IOptions<BotsomeOptions>>().Value;
		var responseService = isp.GetRequiredService<ResponseService>();
		var logger = loggerFactory.CreateLogger<BotsomeClient>();

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