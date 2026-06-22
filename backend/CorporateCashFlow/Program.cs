using CorporateCashFlow.API;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();

var startupInstance = new Startup(builder.Configuration);
startupInstance.Configure(app, app.Environment);

app.Run();
