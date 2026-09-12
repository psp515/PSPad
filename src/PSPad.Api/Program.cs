using PSPad.Api.Commands;
using PSPad.Api.Endpoints;
using PSPad.Api.Identity;
using PSPad.Api.Sync;
using PSPad.Infrastructure;
using PSPad.Infrastructure.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPSPadInfrastructure(builder.Configuration);
builder.Services.AddPSPadCommands();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<SyncReader>();

var app = builder.Build();

app.MapGet("/health", () => "healthy");
app.MapCommandEndpoints();
app.MapSyncEndpoints();
app.MapTodayEndpoints();

await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);

app.Run();

public partial class Program;
