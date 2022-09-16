using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Botsome;

/// <summary>
/// Receives events from a <see cref="DiscordClient"/> and passes them to <see cref="ClientEventService"/>, and carries out responses for CES.
/// </summary>
public class BotsomeClient : IAsyncDisposable {
	private readonly DiscordClient m_Discord;
	private readonly IDisposable m_OnChangeListener;
	private readonly Dictionary<string, DiscordEmoji> m_EmotesByName;
	private readonly Dictionary<ulong, DiscordEmoji> m_EmotesById;

	public Bot Bot { get; }

	private BotsomeClient(Bot bot, DiscordClient discord, ILogger<BotsomeClient> logger, ClientEventService clientEventService, IOptionsMonitor<StatusOptions> statusOptions) {
		m_EmotesByName = new Dictionary<string, DiscordEmoji>();
		m_EmotesById = new Dictionary<ulong, DiscordEmoji>();
		m_Discord = discord;
		Bot = bot;

		async Task UpdateStatus(StatusOptions newOptions) {
			BotActivity? activity = null;
			
			foreach (string group in bot.ParsedGroups) {
				if (newOptions.Groups.TryGetValue(group, out activity)) {
					break;
				}
			}

			if (activity != null) {
				await m_Discord.UpdateStatusAsync(new DiscordActivity(activity.Message, activity.Type));
			} else {
				await m_Discord.UpdateStatusAsync();
			}
		}
		
		m_OnChangeListener = statusOptions.OnChange(newOptions => UpdateStatus(newOptions));

		discord.MessageCreated += (_, ea) => {
			if (!ea.Author.IsBot) {
				clientEventService.OnMessageCreated(this, ea);
			}
			return Task.CompletedTask;
		};

		discord.Ready += (_, _) => {
			Task.Run(async () => {
				await UpdateStatus(statusOptions.CurrentValue);
				
				try {
					foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
						IReadOnlyList<DiscordGuildEmoji> guildEmotes = await kvp.Value.GetEmojisAsync();
						foreach (DiscordGuildEmoji emote in guildEmotes) {
							m_EmotesByName[emote.Name] = emote;
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
		var options = isp.GetRequiredService<IOptionsMonitor<StatusOptions>>();

		await discord.ConnectAsync();

		return new BotsomeClient(bot, discord, logger, clientEventService, options);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
		m_OnChangeListener.Dispose();
	}

	public async Task RespondAsync(EventIdentifier eventIdentifier, BotsomeItem item) {
		DiscordChannel channel = await m_Discord.GetChannelAsync(eventIdentifier.ChannelId);
		foreach (BotsomeResponse response in item.Responses) {
			DiscordEmoji discordEmoji = null!;
			if (response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction) {
				discordEmoji = m_EmotesByName[response.Response];
				
			}

			await (response.Type switch {
				ResponseType.EmojiAsReaction => (await channel.GetMessageAsync(eventIdentifier.MessageId)).CreateReactionAsync(DiscordEmoji.FromUnicode(response.Response)),
				ResponseType.EmoteNameAsReaction => (await channel.GetMessageAsync(eventIdentifier.MessageId)).CreateReactionAsync(discordEmoji),
				ResponseType.EmoteNameAsMessage => channel.SendMessageAsync(discordEmoji),
				ResponseType.Message => channel.SendMessageAsync(response.Response),
				//_ => throw new ArgumentOutOfRangeException()
			});
		}
	}

	public bool CanRespond(BotsomeItem botsomeItem) {
		IEnumerable<string> requiredEmotes = botsomeItem.Responses
			.Where(response => response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction)
			.Select(response => response.Response);
				
		// TODO check if we have the permissions to respond in the channel/guild
		return requiredEmotes.All(emote => m_EmotesByName.ContainsKey(emote));
	}
}