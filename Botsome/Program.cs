using Botsome;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<Random>();
builder.Services.AddSingleton<ClientService>();
builder.Services.AddSingleton<ResponseService>();

builder.Services.Configure<BotsomeOptions>(builder.Configuration.GetSection("Botsome"));

var app = builder.Build();

app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
	app.UseSwagger();
	app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

TaskScheduler.UnobservedTaskException += (sender, eventArgs) => app.Services.GetRequiredService<ILogger<Program>>().LogCritical(eventArgs.Exception, "Unobserved task exception {sender} {observed}", sender, eventArgs.Observed);

app.Run();
