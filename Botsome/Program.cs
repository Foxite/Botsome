using System.Runtime.InteropServices;
using Botsome;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

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
	var bots = JsonConvert.DeserializeObject<Bot[]>(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("BOTSOME_FILE") ?? "/botsome/bots.json"))!;
	await clientService.UpdateList(bots);
}

PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => {
	try {
		UpdateBotsAsync().GetAwaiter().GetResult();
	} catch (Exception e) {
		app.Services.GetRequiredService<ILogger<Program>>().LogError(e, "Update failed");
	}
});

try {
	await UpdateBotsAsync();
} catch (Exception e) {
	app.Services.GetRequiredService<ILogger<Program>>().LogError(e, "Initial update failed");
}

await app.RunAsync();
