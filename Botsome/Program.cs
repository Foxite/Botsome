using Botsome;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hbc, isc) => {
	isc.AddSingleton<Random>();
	isc.AddSingleton<ClientService>();
	isc.AddSingleton<ResponseService>();

	isc.Configure<BotsomeOptions>(hbc.Configuration.GetSection("Botsome"));

	isc.AddSingleton(isp => {
		var dockerConfig = new DockerClientConfiguration();
		return dockerConfig.CreateClient();
	});

	isc.AddHostedService<DockerWatcher>();
});

IHost app = builder.Build();

TaskScheduler.UnobservedTaskException += (sender, eventArgs) => app.Services.GetRequiredService<ILogger<Program>>().LogCritical(eventArgs.Exception, "Unobserved task exception {Sender} {Observed}", sender, eventArgs.Observed);

await app.RunAsync();
