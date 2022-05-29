using System.Runtime.InteropServices;
using Botsome;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.ConfigureServices((hbc, isc) => {
	isc.AddSingleton<Random>();
	isc.AddSingleton<ClientService>();
	isc.AddSingleton<ResponseService>();

	isc.Configure<BotsomeOptions>(hbc.Configuration.GetSection("Botsome"));
});

IHost app = builder.Build();

TaskScheduler.UnobservedTaskException += (sender, eventArgs) => app.Services.GetRequiredService<ILogger<Program>>().LogCritical(eventArgs.Exception, "Unobserved task exception {sender} {observed}", sender, eventArgs.Observed);

async Task UpdateBotsAsync() {
	var clientService = app.Services.GetRequiredService<ClientService>();
	IEnumerable<(string Id, string Token)> botStrings = JObject.Parse(await File.ReadAllTextAsync("/botsome/bots.json")).Properties().Select(prop => (Id: prop.Name, Token: prop.Value.ToObject<string>()!));
	await clientService.UpdateList(botStrings);
}

PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => Task.Run(UpdateBotsAsync));

await UpdateBotsAsync();

await app.RunAsync();
