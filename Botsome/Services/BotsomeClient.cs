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
	private ResponseService m_ResponseService;
	private readonly Dictionary<string, Dictionary<ulong, DiscordEmoji>> m_Emotes;

	public string Token { get; }
	public string Id { get; }
	public string[] Groups { get; set; }

	// ReSharper disable warning CS8618
	private BotsomeClient(Bot bot, DiscordClient discord, IOptionsMonitor<BotsomeOptions> options, ResponseService responseService, ILogger<BotsomeClient> logger) {
		m_Emotes = new Dictionary<string, Dictionary<ulong, DiscordEmoji>>();
		m_Discord = discord;
		m_Options = options;
		m_ResponseService = responseService;
		Token = bot.Token;
		Id = bot.Id;
		Groups = bot.ParsedGroups;

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

					foreach ((ulong guildId, DiscordGuild guild) in m_Discord.Guilds) {
						IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await guild.GetEmojisAsync();
						// ReSharper disable warning CS8601
						foreach (string emoteName in emoteNames) {
							if (m_Emotes.ContainsKey(emoteName)) {
								continue;
							}

							DiscordEmoji? emoji = guildEmojis.FirstOrDefault(emoji => emoji.Name == emoteName);
							if (emoji != null) {
								m_Emotes.TryAdd(emoteName, new Dictionary<ulong, DiscordEmoji>());
								m_Emotes[emoteName][emoji.Id] = emoji;
							}
						}
					}

					if (emoteNames.Count != m_Emotes.Count) {
						logger.LogCritical("Did not find emotes with names: {Names}", string.Join(", ", emoteNames.Where(emoteName => !m_Emotes.ContainsKey(emoteName))));
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
			if (!ea.Author.IsBot && AllowChannel(item, ea.Channel)) {
				bool report = false;
				string? data = null;
				switch (item.Trigger.Type) {
					case TriggerType.MessageContent:
						report = item.Trigger.ActualMessageRegex!.IsMatch(ea.Message.Content);
						break;
					case TriggerType.EmoteNameAsMessage:
						foreach (Match match in EmoteRegex.Matches(ea.Message.Content)) {
							string emoteName = match.Groups["name"].Value;
							if (item.Trigger.ActualEmoteNameRegex!.IsMatch(emoteName)) {
								report = true;
								data = match.Groups["id"].Value;
								break;
							}
						}
						break;
				}

				if (report) {
					await m_ResponseService.ReportAsync(new BotsomeEvent(ea.Channel.Id, ea.Message.Id, item, data), this);
				}
			}
		}
	}

	private static bool AllowChannel(BotsomeItem item, DiscordChannel channel) {
		return item.Trigger.OnlyInChannels is not { Count: > 0 } || item.Trigger.OnlyInChannels.Contains(channel.Id);
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

		await discord.ConnectAsync();

		return new BotsomeClient(bot, discord, options, responseService, logger);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
	}

	public async Task RespondAsync(BotsomeEvent evt) {
		DiscordChannel channel = await m_Discord.GetChannelAsync(evt.ChannelId);
		foreach (BotsomeResponse response in evt.Item.Responses) {
			switch (response.Type) {
				case ResponseType.EmojiAsReaction:
					await (await channel.GetMessageAsync(evt.MessageId)).CreateReactionAsync(DiscordEmoji.FromUnicode(response.Response));
					break;
				case ResponseType.EmoteNameAsReaction: {
					var emotes = m_Emotes[response.Response];
					if (!emotes.TryGetValue(ulong.Parse(evt.Data!), out DiscordEmoji? emote)) {
						emote = emotes.First().Value;
					}
					await (await channel.GetMessageAsync(evt.MessageId)).CreateReactionAsync(emote);
					break;
				}
				case ResponseType.EmoteNameAsMessage: {
					var emotes = m_Emotes[response.Response];
					if (!emotes.TryGetValue(ulong.Parse(evt.Data!), out DiscordEmoji? emote)) {
						emote = emotes.First().Value;
					}

					await channel.SendMessageAsync(emote);
					break;
				}
				case ResponseType.Message:
					await channel.SendMessageAsync(response.Response);
					break;
			}
		}
	}
}