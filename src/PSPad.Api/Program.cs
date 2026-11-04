using Microsoft.AspNetCore.Authentication.JwtBearer;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Api.Cors;
using PSPad.Api.Endpoints;
using PSPad.Api.Identity;
using PSPad.Api.Live;
using PSPad.Api.Presentation;
using PSPad.Api.Proxy;
using PSPad.Api.Quotes;
using PSPad.Api.Rates;
using PSPad.Api.RateLimiting;
using PSPad.Api.Snapshots;
using PSPad.Api.Statistics;
using PSPad.Api.Sync;
using PSPad.Infrastructure;
using PSPad.Infrastructure.Events;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Presentation;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Statistics;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1024 * 1024);

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
        options.Events = AuthenticationLogging.Events();
    });
builder.Services.AddAuthorization();

builder.Services.AddPSPadForwardedHeaders();
builder.Services.AddPSPadRateLimiter(builder.Configuration);

builder.Services.AddPSPadCors();

builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.Configure<KeycloakAdminOptions>(builder.Configuration.GetSection(KeycloakAdminOptions.Section));
builder.Services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();
builder.Services.Configure<AccountDeletionOptions>(builder.Configuration.GetSection(AccountDeletionOptions.Section));
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
builder.Services.AddScoped<IInboxRecordStore, MongoInboxRecordStore>();
builder.Services.AddScoped<IProjectionMarker, MongoProjectionMarker>();
builder.Services.AddScoped<IStatisticsReset, MongoStatisticsReset>();
builder.Services.AddScoped<ITaskSnapshotSource, MongoTaskSnapshotSource>();
builder.Services.AddScoped<IViewTargets, StoreViewTargets>();
builder.Services.AddScoped<IDomainEventHandler, StatisticsRecordProjection>();
builder.Services.AddScoped<IDomainEventHandler, StatisticsLabelProjection>();
builder.Services.AddScoped<IDomainEventHandler, InboxRecordProjection>();
builder.Services.AddScoped<IDomainEventReplay, StatisticsReplay>();
builder.Services.AddScoped<StatisticsReader>();
builder.Services.AddScoped<StatisticsOverviewReader>();
builder.Services.AddHostedService<DomainEventPump>();

builder.Services.AddScoped<ISnapshotStore, MongoSnapshotStore>();
builder.Services.AddScoped<ISnapshotVisitStore, MongoSnapshotVisitStore>();
builder.Services.AddScoped<IListContent, MongoListContent>();
builder.Services.AddScoped<IServerCommands, DispatcherServerCommands>();
builder.Services.AddScoped<SnapshotPublishing>();
builder.Services.AddScoped<IMarkBudget, MongoMarkBudget>();
builder.Services.AddScoped<SnapshotMarking>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<NbpRateClient>(client =>
{
    client.BaseAddress = new Uri("https://api.nbp.pl/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<NbpSeriesClient>(client =>
{
    client.BaseAddress = new Uri("https://api.nbp.pl/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<YahooQuoteClient>(client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PSPad)");
});
builder.Services.AddScoped<QuoteCache>();

builder.Services.Configure<LiveOptions>(builder.Configuration.GetSection(LiveOptions.Section));
builder.Services.AddSingleton<LiveConnections>();
builder.Services.AddScoped<IContainerMembers, MongoContainerMembers>();
builder.Services.AddScoped<LiveAudience>();
builder.Services.AddScoped<ICommitObserver, LiveNudges>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors(CorsRegistration.Policy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => "healthy");

var api = app.MapGroup("/api").RequireAuthorization();
api.MapCommandEndpoints();
api.MapSyncEndpoints();
api.MapLiveEndpoints();
api.MapListEndpoints();
api.MapBudgetEndpoints();
api.MapTodayEndpoints();
api.MapMeEndpoints();
api.MapAccountEndpoints();
api.MapStatisticsEndpoints();
api.MapSnapshotEndpoints();
api.MapMoneyEndpoints();
api.MapInvestmentEndpoints();

var open = app.MapGroup("/api/public").AllowAnonymous().RequireRateLimiting("public");
open.MapPublicSnapshotEndpoints();

await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);
await MongoBackfill.EnsureCreatedAtAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);
await MongoBackfill.EnsureGoalLinksAsync(app.Services.GetRequiredService<MongoContext>(), CancellationToken.None);

app.Run();

public partial class Program;
