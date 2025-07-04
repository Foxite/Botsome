using Botsome;
using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((hbc, icb) => {
	icb.AddYamlFile("appsettings.yaml", true);
	icb.AddYamlFile($"appsettings.{hbc.HostingEnvironment.EnvironmentName}.yaml", true);
});

builder.ConfigureServices((hbc, isc) => {
	isc.AddSingleton<Random>();
	isc.AddSingleton<ClientService>();
	isc.AddSingleton<ClientEventService>();
	isc.AddSingleton<ItemsService, ConfigItemsService>();
	isc.AddSingleton<NotificationService>();

	isc.Configure<BotsomeOptions>(hbc.Configuration.GetSection("Botsome"));
	isc.Configure<StatusOptions>(hbc.Configuration.GetSection("Status"));
	isc.Configure<List<BotsomeItem>>(hbc.Configuration.GetSection("Items"));
	isc.Configure<NotificationService.Config>(hbc.Configuration.GetSection("Notifications"));

	isc.AddSingleton(isp => {
		var dockerConfig = new DockerClientConfiguration();
		return dockerConfig.CreateClient();
	});

	isc.AddHostedService<DockerWatcher>();
});

IHost app = builder.Build();

TaskScheduler.UnobservedTaskException += (sender, eventArgs) => app.Services.GetRequiredService<ILogger<Program>>().LogCritical(eventArgs.Exception, "Unobserved task exception {Sender} {Observed}", sender, eventArgs.Observed);

await app.RunAsync();
