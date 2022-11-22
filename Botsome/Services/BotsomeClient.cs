using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Botsome;

public class BotsomeClient : IAsyncDisposable {
	private static readonly Regex EmoteRegex = new Regex(@"<(?<animated>a?):(?<name>\w+):(?<id>\d{5,23})>");
	
	private readonly DiscordClient m_Discord;
	private readonly IOptionsMonitor<BotsomeOptions> m_Options;
	private readonly ResponseService m_ResponseService;
	private readonly IDisposable m_OnChangeListener;

	public Dictionary<string, DiscordEmoji> Emotes { get; }
	public string Token { get; }
	public string Id { get; }
	public string[] Groups { get; }

	// ReSharper disable warning CS8618
	private BotsomeClient(Bot bot, DiscordClient discord, IOptionsMonitor<BotsomeOptions> options, ResponseService responseService, ILogger<BotsomeClient> logger, Random random) {
		Emotes = new Dictionary<string, DiscordEmoji>();
		m_Discord = discord;
		m_Options = options;
		m_ResponseService = responseService;
		Token = bot.Token;
		Id = bot.Id;
		Groups = bot.ParsedGroups;

		async Task UpdateStatus(BotsomeOptions newOptions) {
			BotActivity? activity = null;
			
			foreach (string group in bot.ParsedGroups) {
				if (newOptions.Status.TryGetValue(group, out var activities)) {
					activity = activities[random.Next(0, activities.Length)];
					break;
				}
			}

			if (activity != null) {
				await m_Discord.UpdateStatusAsync(new DiscordActivity(activity.Message, activity.Type));
			} else {
				await m_Discord.UpdateStatusAsync();
			}
		}
		
		m_OnChangeListener = options.OnChange(newOptions => UpdateStatus(newOptions));

		discord.MessageCreated += (client, ea) => {
			_ = Task.Run(async () => {
				try {
					await OnMessageAsync(ea);
				} catch (Exception ex) {
					logger.LogError(ex, "Exception caught in off-thread MessageCreated handler");
				}
			});
			return Task.CompletedTask;
		};

		discord.Ready += (_, _) => {
			Task.Run(async () => {
				await UpdateStatus(options.CurrentValue);
				
				try {
					List<string> emoteNames = 
						(
							from item in options.CurrentValue.Items
							from response in item.Responses
							where response.Type is ResponseType.EmoteNameAsMessage or ResponseType.EmoteNameAsReaction && response.Response != null
							select response.Response
						)
						.Distinct()
						.ToList();

					foreach (KeyValuePair<ulong, DiscordGuild> kvp in m_Discord.Guilds) {
						IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await kvp.Value.GetEmojisAsync();
						// ReSharper disable warning CS8601
						foreach (string emoteName in emoteNames) {
							if (Emotes.ContainsKey(emoteName)) {
								continue;
							}

							DiscordEmoji? emoji = guildEmojis.FirstOrDefault(emoji => emoji.Name == emoteName);
							if (emoji != null) {
								Emotes[emoteName] = emoji;
							}
						}
					}

					if (emoteNames.Count != Emotes.Count) {
						logger.LogCritical("Did not find emotes with names: {Names}", string.Join(", ", emoteNames.Where(emoteName => !Emotes.ContainsKey(emoteName))));
					}
				} catch (Exception ex) {
					logger.LogCritical(ex, "Exception caught while collecting emotes");
				}
			});
			return Task.CompletedTask;
		};
	}

	public async Task OnMessageAsync(MessageCreateEventArgs ea) {
		foreach (BotsomeItem item in m_Options.CurrentValue.Items) {
			if (!ea.Author.IsBot && AllowChannel(item, ea.Channel) && item.Trigger.Type switch {
			    TriggerType.MessageContent => item.Trigger.ActualMessageRegex!.IsMatch(ea.Message.Content),
			    TriggerType.EmoteNameAsMessage => EmoteRegex.Matches(ea.Message.Content).Select(match => match.Groups["name"].Value).Any(emoteName => item.Trigger.ActualEmoteNameRegex!.IsMatch(emoteName)),
				TriggerType.MessageFromUser => item.Trigger.UserId == ea.Author.Id,
			    _ => false
		    }) {
				await m_ResponseService.ReportAsync(new BotsomeEvent(ea.Channel.Id, ea.Message.Id, item), this);
			}
		}
	}

	private static bool AllowChannel(BotsomeItem item, DiscordChannel channel) {
		return (item.Trigger.OnlyInChannels is not { Count: > 0 } || item.Trigger.OnlyInChannels.Contains(channel.Id))
			&& (!channel.GuildId.HasValue || item.Trigger.OnlyInServers is not { Count: > 0 } || item.Trigger.OnlyInServers.Contains(channel.GuildId.Value));
	}

	public static async Task<BotsomeClient> CreateAsync(Bot bot, IServiceProvider isp) {
		ILoggerFactory loggerFactory = isp.GetRequiredService<ILoggerFactory>().Scope("ID: {Id}", bot.Id);
		
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = bot.Token,
			Intents = DiscordIntents.GuildMessages,
			LoggerFactory = loggerFactory
		});

		var options = isp.GetRequiredService<IOptionsMonitor<BotsomeOptions>>();
		var responseService = isp.GetRequiredService<ResponseService>();
		var logger = loggerFactory.CreateLogger<BotsomeClient>();
		var random = isp.GetRequiredService<Random>();

		await discord.ConnectAsync();

		return new BotsomeClient(bot, discord, options, responseService, logger, random);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
		m_OnChangeListener.Dispose();
	}

	public async Task RespondAsync(BotsomeEvent evt) {
		DiscordChannel channel = await m_Discord.GetChannelAsync(evt.ChannelId);
		foreach (BotsomeResponse response in evt.Item.Responses) {
			await (response.Type switch {
				ResponseType.EmojiAsReaction => (await channel.GetMessageAsync(evt.MessageId)).CreateReactionAsync(DiscordEmoji.FromUnicode(response.Response)),
				ResponseType.EmoteNameAsReaction => (await channel.GetMessageAsync(evt.MessageId)).CreateReactionAsync(Emotes[response.Response]),
				ResponseType.EmoteNameAsMessage => channel.SendMessageAsync(Emotes[response.Response]),
				ResponseType.Message => channel.SendMessageAsync(response.Response),
				//_ => throw new ArgumentOutOfRangeException()
			});
		}
	}
}