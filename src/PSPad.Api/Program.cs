using Microsoft.AspNetCore.Authentication.JwtBearer;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Api.Endpoints;
using PSPad.Api.Identity;
using PSPad.Api.Statistics;
using PSPad.Api.Sync;
using PSPad.Infrastructure;
using PSPad.Infrastructure.Events;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPSPadInfrastructure(builder.Configuration);
builder.Services.AddPSPadCommands();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata") ?? !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("AppClient", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.Configure<KeycloakAdminOptions>(builder.Configuration.GetSection(KeycloakAdminOptions.Section));
builder.Services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();
builder.Services.AddScoped<UserProvisioner>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<SyncReader>();
// Two registrations of one instance: a second would hand the pump a channel nothing writes to.
builder.Services.AddSingleton<ChannelDomainEventDispatcher>();
builder.Services.AddSingleton<IDomainEventDispatcher>(sp =>
    sp.GetRequiredService<ChannelDomainEventDispatcher>());
builder.Services.AddScoped<IEventLog, MongoEventLog>();
builder.Services.AddScoped<IStatisticsStore, MongoStatisticsStore>();
builder.Services.AddScoped<ILabelStore, MongoLabelStore>();
builder.Services.AddScoped<IProjectionMarker, MongoProjectionMarker>();
builder.Services.AddScoped<ITaskSnapshotSource, MongoTaskSnapshotSource>();
builder.Services.AddScoped<IDomainEventHandler, StatisticsRecordProjection>();
builder.Services.AddScoped<IDomainEventHandler, StatisticsLabelProjection>();
builder.Services.AddScoped<IDomainEventReplay, StatisticsReplay>();
builder.Services.AddScoped<StatisticsReader>();
builder.Services.AddScoped<StatisticsOverviewReader>();
builder.Services.AddHostedService<DomainEventPump>();

var app = builder.Build();

app.UseCors("AppClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "healthy");

var api = app.MapGroup("/api").RequireAuthorization();
api.MapCommandEndpoints();
api.MapSyncEndpoints();
api.MapTodayEndpoints();
api.MapMeEndpoints();
api.MapAccountEndpoints();
api.MapStatisticsEndpoints();

await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);
await MongoBackfill.EnsureCreatedAtAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);

app.Run();

public partial class Program;
