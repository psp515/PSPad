using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.AddScoped<UserProvisioner>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<SyncReader>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "healthy");

var api = app.MapGroup("/api").RequireAuthorization();
api.MapCommandEndpoints();
api.MapSyncEndpoints();
api.MapTodayEndpoints();
api.MapMeEndpoints();

await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);

app.Run();

public partial class Program;
