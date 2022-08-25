using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Botsome;

public class BotsomeClient : IAsyncDisposable {
	private readonly DiscordClient m_Discord;

	public Dictionary<string, DiscordEmoji> Emotes { get; }
	public string Token { get; }
	public string Id { get; }
	public string[] Groups { get; }

	private BotsomeClient(Bot bot, DiscordClient discord, ILogger<BotsomeClient> logger, ClientEventService clientEventService) {
		Emotes = new Dictionary<string, DiscordEmoji>();
		m_Discord = discord;
		Token = bot.Token;
		Id = bot.Id;
		Groups = bot.ParsedGroups;

		discord.MessageCreated += (_, ea) => {
			if (!ea.Author.IsBot) {
				clientEventService.OnMessageCreated(this, ea);
			}

			return Task.CompletedTask;
		};

		discord.Ready += (_, _) => {
			Task.Run(async () => {
				try {
					foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
						IReadOnlyList<DiscordGuildEmoji>? guildEmotes = await kvp.Value.GetEmojisAsync();
						// ReSharper disable warning CS8601
						foreach (DiscordGuildEmoji emote in guildEmotes) {
							Emotes[emote.Name] = emote;
						}
					}
				} catch (Exception ex) {
					logger.LogCritical(ex, "Exception caught while collecting emotes");
				}
			});
			return Task.CompletedTask;
		};
	}

	public static async Task<BotsomeClient> CreateAsync(Bot bot, IServiceProvider isp) {
		ILoggerFactory loggerFactory = isp.GetRequiredService<ILoggerFactory>().Scope("ID: {Id}", bot.Id);
		
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = bot.Token,
			Intents = DiscordIntents.GuildMessages,
			LoggerFactory = loggerFactory
		});

		var clientEventService = isp.GetRequiredService<ClientEventService>();
		var logger = loggerFactory.CreateLogger<BotsomeClient>();

		await discord.ConnectAsync();

		return new BotsomeClient(bot, discord, logger, clientEventService);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
	}

	public async Task RespondAsync(EventIdentifier eventIdentifier, BotsomeItem item) {
		DiscordChannel channel = await m_Discord.GetChannelAsync(eventIdentifier.ChannelId);
		foreach (BotsomeResponse response in item.Responses) {
			await (response.Type switch {
				ResponseType.EmojiAsReaction => (await channel.GetMessageAsync(eventIdentifier.MessageId)).CreateReactionAsync(DiscordEmoji.FromUnicode(response.Response)),
				ResponseType.EmoteNameAsReaction => (await channel.GetMessageAsync(eventIdentifier.MessageId)).CreateReactionAsync(Emotes[response.Response]),
				ResponseType.EmoteNameAsMessage => channel.SendMessageAsync(Emotes[response.Response]),
				ResponseType.Message => channel.SendMessageAsync(response.Response),
				//_ => throw new ArgumentOutOfRangeException()
			});
		}
	}

	public bool CanRespond(BotsomeItem botsomeItem) {
		IEnumerable<string> requiredEmotes = botsomeItem.Responses
			.Where(response => response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction)
			.Select(response => response.Response);
				
		return requiredEmotes.All(emote => Emotes.ContainsKey(emote));
	}
}