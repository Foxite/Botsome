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
	private readonly NotificationService m_NotificationService;
	private readonly IDisposable m_OnChangeListener;
	private readonly Dictionary<string, DiscordEmoji> m_EmotesByName;
	private readonly Dictionary<ulong, DiscordEmoji> m_EmotesById;

	public Bot Bot { get; }

	private BotsomeClient(Bot bot, DiscordClient discord, ILogger<BotsomeClient> logger, ClientEventService clientEventService, IOptionsMonitor<StatusOptions> statusOptions, Random random, NotificationService notificationService) {
		m_EmotesByName = new Dictionary<string, DiscordEmoji>();
		m_EmotesById = new Dictionary<ulong, DiscordEmoji>();
		m_Discord = discord;
		m_NotificationService = notificationService;
		Bot = bot;

		async Task UpdateStatus(StatusOptions newOptions) {
			BotActivity? activity = null;
			
			foreach (string group in bot.ParsedGroups) {
				if (newOptions.Groups.TryGetValue(group, out BotActivity[]? activities)) {
					activity = activities[random.Next(0, activities.Length)];
					break;
				}
			}

			if (activity != null && activity.Type.HasValue) {
				await m_Discord.UpdateStatusAsync(new DiscordActivity(activity.Message, activity.Type.Value));
			} else {
				await m_Discord.UpdateStatusAsync();
			}
		}
		
		m_OnChangeListener = statusOptions.OnChange(newOptions => UpdateStatus(newOptions));

		discord.MessageCreated += (_, ea) => {
			// Note: this is a temporary diagnostic for issue #17
			if (!ea.Channel.GuildId.HasValue && (ea.Guild != null || ea.Channel.Guild != null || ea.Channel.Type is not (ChannelType.Group or ChannelType.Private))) {
				notificationService.SendNotification($"Channel object missing guildId when receiving message: {ea.Guild?.Id}/{ea.Channel?.Id}/{ea.Message?.Id} by {ea.Author?.Id} {ea.Message?.JumpLink}");
				return Task.CompletedTask;
			}
			
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
							m_EmotesById[emote.Id] = emote;
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
		var random = isp.GetRequiredService<Random>();
		var notificationService = isp.GetRequiredService<NotificationService>();

		await discord.ConnectAsync();

		return new BotsomeClient(bot, discord, logger, clientEventService, options, random, notificationService);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
		m_OnChangeListener.Dispose();
	}

	public async Task RespondAsync(EventIdentifier eventIdentifier, IEnumerable<BotsomeResponse> responses, ulong? emoteId) {
		DiscordChannel channel = await m_Discord.GetChannelAsync(eventIdentifier.ChannelId);
		foreach (BotsomeResponse response in responses) {
			DiscordEmoji? discordEmoji = null;
			if (response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction) {
				if (!(emoteId.HasValue && m_EmotesById.TryGetValue(emoteId.Value, out discordEmoji))) {
					discordEmoji = m_EmotesByName[response.Response];
				}
			}

			DiscordMessage message;
			switch (response.Type) {
				case ResponseType.EmojiAsReaction:
					message = await channel.GetMessageAsync(eventIdentifier.MessageId);
					await message.CreateReactionAsync(DiscordEmoji.FromUnicode(response.Response));
					break;
				case ResponseType.EmoteNameAsReaction:
					message = await channel.GetMessageAsync(eventIdentifier.MessageId);
					await message.CreateReactionAsync(discordEmoji);
					break;
				case ResponseType.EmoteNameAsMessage:
					await channel.SendMessageAsync(discordEmoji);
					break;
				case ResponseType.Message:
					await channel.SendMessageAsync(dmb => dmb.WithContent(response.Response).WithReply(eventIdentifier.MessageId, false, false));
					break;
				case ResponseType.Notification:
					await Task.Run(() => m_NotificationService.SendNotification($"A botsome item was triggered by {eventIdentifier.ChannelId}/{eventIdentifier.MessageId} {response.Response}"));
					break;
				case ResponseType.Sticker:
					var sticker = await m_Discord.GetStickerAsync(ulong.Parse(response.Response));
					await channel.SendMessageAsync(dmb => dmb.WithSticker(sticker));
			break;
				//ResponseType.Sticker => channel.SendMessageAsync(dmb => dmb.WithSticker(await m_Discord.GetStickerAsync(ulong.Parse(response.Response))))
				//_ => throw new ArgumentOutOfRangeException()
			});
		}
	}

	public ICollection<BotsomeResponse> CanRespond(BotsomeItem botsomeItem, ICollection<BotsomeResponse> responses) {
		if (!(string.IsNullOrWhiteSpace(botsomeItem.RespondGroup) || botsomeItem.RespondGroup == "all") && !Bot.ParsedGroups.Contains(botsomeItem.RespondGroup)) {
			return Array.Empty<BotsomeResponse>();
		}

		return responses
			.Where(response => {
				if (response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction) {
					if (!m_EmotesByName.ContainsKey(response.Response)) {
						return false;
					}

					// TODO check if the bot has AddReactions permission
					bool hasPermission = true;
					if (!hasPermission) {
						return false;
					}
				}

				if (response.Type is ResponseType.Message or ResponseType.EmoteNameAsMessage) {
					// TODO check if the bot has SendMessages permission
					bool hasPermission = true;
					if (!hasPermission) {
						return false;
					}
				}

				return true;
			})
			.ToList();
	}
}
