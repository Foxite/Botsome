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

	// ReSharper disable warning CS8618
	// TODO remove itemList parameter, assemble a list of all accessible guild emotes by name
	private BotsomeClient(string token, DiscordClient discord, IOptions<ICollection<BotsomeItem>> itemList, string id, ILogger<BotsomeClient> logger, ClientEventService clientEventService) {
		Emotes = new Dictionary<string, DiscordEmoji>();
		m_Discord = discord;
		Token = token;
		Id = id;

		discord.MessageCreated += (_, ea) => {
			clientEventService.OnMessageCreated(this, ea);
			return Task.CompletedTask;
		};

		discord.Ready += (_, _) => {
			Task.Run(async () => {
				try {
					List<string> emoteNames = 
						(
							from item in itemList.Value
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

	public static async Task<BotsomeClient> CreateAsync(string token, string id, IServiceProvider isp) {
		ILoggerFactory loggerFactory = isp.GetRequiredService<ILoggerFactory>().Scope("ID: {Id}", id);
		
		var discord = new DiscordClient(new DiscordConfiguration() {
			Token = token,
			Intents = DiscordIntents.GuildMessages,
			LoggerFactory = loggerFactory
		});

		var options = isp.GetRequiredService<IOptions<ICollection<BotsomeItem>>>();
		var clientEventService = isp.GetRequiredService<ClientEventService>();
		var logger = loggerFactory.CreateLogger<BotsomeClient>();

		await discord.ConnectAsync();

		return new BotsomeClient(token, discord, options, id, logger, clientEventService);
	}
	
	public async ValueTask DisposeAsync() {
		await m_Discord.DisconnectAsync();
		m_Discord.Dispose();
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