using System.Text;
using System.Text.RegularExpressions;
using ChessBot;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Shared;

static string Envvar(string name, string? def = null) => Environment.GetEnvironmentVariable(name) ?? def ?? throw new InvalidOperationException($"Envvar {name} is not set");

string coordinatorUrl = Envvar("COORDINATOR_URL");
string botToken = Envvar(Envvar("BOT_TOKEN_ENVVAR", "BOT_TOKEN"));
ulong emoteId = ulong.Parse(Envvar("EMOTE"));

var sessionGuid = Guid.NewGuid();
var emoteRegex = new Regex(@"^<a?:\w+:(?<id>\d{18})>$");
var http = new HttpClient();
var events = new NdJsonClient(http);
var discord = new DiscordClient(new DiscordConfiguration() {
	Token = botToken,
	Intents = DiscordIntents.GuildMessages
});

_ = Task.Run(async () => {
	DiscordEmoji? discordEmoji = null;
	try {
		await foreach (BotsomeEvent evt in events.StreamLinesAsync(new HttpRequestMessage(HttpMethod.Get, coordinatorUrl + $"/Botsome?id={sessionGuid}"), true, line => JsonConvert.DeserializeObject<BotsomeEvent>(line)!)) {
			if (discordEmoji == null && !DiscordEmoji.TryFromGuildEmote(discord, emoteId, out discordEmoji)) {
				foreach (KeyValuePair<ulong, DiscordGuild> kvp in discord.Guilds) {
					IReadOnlyList<DiscordGuildEmoji>? guildEmojis = await kvp.Value.GetEmojisAsync();
					discordEmoji = guildEmojis.FirstOrDefault(emoji => emoji.Id == emoteId);
					if (discordEmoji != null) {
						break;
					}
				}
			}

			if (discordEmoji == null) {
				Console.WriteLine("Did not find emote");
				return;
			}
			
			DiscordChannel channel = await discord.GetChannelAsync(evt.ChannelId);
			DiscordMessage message = await channel.GetMessageAsync(evt.MessageId);
			await message.CreateReactionAsync(discordEmoji);
		}
	} catch (Exception e) {
		Console.WriteLine(e);
	}
});

discord.MessageCreated += async (o, e) => {
	Match match = emoteRegex.Match(e.Message.Content);
	if (match.Success && match.Groups["id"].Value == emoteId.ToString()) {
		HttpResponseMessage msg = await http.PostAsync(coordinatorUrl + $"/Botsome?id={sessionGuid}", new StringContent(JsonConvert.SerializeObject(new BotsomeEvent(e.Channel.Id, e.Message.Id)), Encoding.UTF8, "application/json"));
		msg.EnsureSuccessStatusCode();
	}
};

await discord.ConnectAsync();
await Task.Delay(-1);
