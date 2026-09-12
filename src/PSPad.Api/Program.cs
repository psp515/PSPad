using PSPad.Api.Commands;
using PSPad.Api.Endpoints;
using PSPad.Api.Identity;
using PSPad.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPSPadInfrastructure(builder.Configuration);
builder.Services.AddPSPadCommands();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();
builder.Services.AddScoped<CommandDispatcher>();

var app = builder.Build();

app.MapGet("/health", () => "healthy");
app.MapCommandEndpoints();

app.Run();

public partial class Program;
