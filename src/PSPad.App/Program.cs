using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.State;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Search;
using PSPad.App.State.Viewport;
using PSPad.App.Statistics;
using PSPad.App.Sync;
using PSPad.App.Theme;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("email");
});

var keycloakAuthority = builder.Configuration["Keycloak:Authority"]!;
var keycloakClientId = builder.Configuration["Keycloak:ClientId"]!;

builder.Services.AddSingleton<ILocalSessionStore, LocalSessionStore>();
builder.Services.AddSingleton(services => new TokenRefresher(
    new HttpClient(), services.GetRequiredService<IClock>(), keycloakAuthority, keycloakClientId));
builder.Services.AddScoped<SessionBootstrapper>();

builder.Services.AddScoped<IRemoteAuthenticationService<RemoteAuthenticationState>>(services =>
    services.GetServices<AuthenticationStateProvider>()
        .OfType<IRemoteAuthenticationService<RemoteAuthenticationState>>()
        .Single());

builder.Services.AddScoped<IAccessTokenProvider>(services =>
    services.GetServices<AuthenticationStateProvider>()
        .OfType<IAccessTokenProvider>()
        .Single());

builder.Services.AddSingleton<LocalAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(
    services => services.GetRequiredService<LocalAuthenticationStateProvider>());
builder.Services.AddScoped<SessionAuthorizationHandler>();
builder.Services.AddScoped<LocalSignOut>();
builder.Services.AddScoped<LocalAccountDeletion>();

builder.Services.AddScoped<IReplica, IndexedDbReplica>();
builder.Services.AddScoped<IOutbox, IndexedDbOutbox>();
builder.Services.AddScoped(typeof(IDocumentStore<>), typeof(ReplicaDocumentStore<>));
builder.Services.AddScoped<ReplicaUnitOfWork>();
builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<ReplicaUnitOfWork>());
builder.Services.AddSingleton<IClock, BrowserClock>();
builder.Services.AddScoped<CommandSender>();
builder.Services.AddScoped<ReplicaOwnership>();
builder.Services.AddTransient<IViewport, BrowserViewport>();
builder.Services.AddPSPadCommands();

var apiBaseAddress = builder.Configuration["Api:BaseAddress"]!;

builder.Services.AddSingleton<ServerReachability>();
builder.Services.AddScoped<ServerReachabilityHandler>();

builder.Services.AddHttpClient<PSPadApiClient>(client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<ServerReachabilityHandler>()
    .AddHttpMessageHandler<SessionAuthorizationHandler>();

// An unreachable server is a supported state here, not a fault worth a stack trace per request.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ThemePreference>();
builder.Services.AddScoped<CardCollapseState>();
builder.Services.AddScoped<ReplicaSearch>();
builder.Services.AddScoped<SidebarCounts>();
builder.Services.AddScoped<StatisticsCache>();
builder.Services.AddScoped<IStatisticsSource>(sp => sp.GetRequiredService<PSPadApiClient>());
builder.Services.AddScoped<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
builder.Services.AddScoped<IConnectivity, BrowserConnectivity>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<SyncCoordinator>();
builder.Services.AddScoped<ISyncTrigger>(sp => sp.GetRequiredService<SyncCoordinator>());

var host = builder.Build();
var bootLogger = host.Services.GetRequiredService<ILogger<Program>>();

// Any bootstrap or teardown failure must still reveal the app: a held splash is an unrecoverable
// blank screen, so nothing below is allowed to escape and skip host.RunAsync().
try
{
    var bootstrapper = host.Services.GetRequiredService<SessionBootstrapper>();
    await bootstrapper.StartAsync();
}
catch (Exception exception)
{
    bootLogger.LogWarning("Session bootstrap failed: {Reason}", exception.Message);
}

try
{
    await host.Services.GetRequiredService<IJSRuntime>().InvokeVoidAsync("pspadBoot.done");
}
catch (Exception exception)
{
    bootLogger.LogWarning("Boot splash teardown failed: {Reason}", exception.Message);
}

await host.RunAsync();
