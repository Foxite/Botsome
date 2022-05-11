using System.Text;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Shared;

static string Envvar(string name, string? def = null) => Environment.GetEnvironmentVariable(name) ?? def ?? throw new InvalidOperationException($"Envvar {name} is not set");

string coordinatorUrl = Envvar("COORDINATOR_URL");
string botToken = Envvar(Envvar("BOT_TOKEN_ENVVAR", "BOT_TOKEN"));
ulong emoteId = ulong.Parse(Envvar("EMOTE"));

var sessionGuid = Guid.NewGuid();
var http = new HttpClient();
var events = new NdJsonClient(http);

await Task.Delay(-1);
