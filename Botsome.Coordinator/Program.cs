using Botsome.Coordinator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.UseConsoleLifetime();
builder.ConfigureServices(isc => {
	isc.AddSingleton<Random>();
	isc.AddSingleton<ResponseService>();
});

IHost app = builder.Build();

TaskScheduler.UnobservedTaskException += (sender, eventArgs) => app.Services.GetRequiredService<ILogger<Program>>().LogCritical(eventArgs.Exception, "Unobserved task exception {sender} {observed}", sender, eventArgs.Observed);

app.Run();
