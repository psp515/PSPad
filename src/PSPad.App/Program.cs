using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App;
using PSPad.App.Api;
using PSPad.App.State;
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
});

builder.Services.AddScoped<IReplica, IndexedDbReplica>();
builder.Services.AddScoped<IOutbox, IndexedDbOutbox>();
builder.Services.AddScoped(typeof(IDocumentStore<>), typeof(ReplicaDocumentStore<>));
builder.Services.AddScoped<ReplicaUnitOfWork>();
builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<ReplicaUnitOfWork>());
builder.Services.AddScoped<IClock, BrowserClock>();
builder.Services.AddScoped<CommandSender>();
builder.Services.AddPSPadCommands();

var apiBaseAddress = builder.Configuration["Api:BaseAddress"]!;

builder.Services.AddHttpClient<PSPadApiClient>(client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler(sp =>
    {
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
        handler.ConfigureHandler(authorizedUrls: [apiBaseAddress]);
        return handler;
    });

builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ThemePreference>();
builder.Services.AddScoped<FabContext>();
builder.Services.AddScoped<IHistorySource>(sp => sp.GetRequiredService<PSPadApiClient>());
builder.Services.AddScoped<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
builder.Services.AddScoped<IConnectivity, BrowserConnectivity>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<SyncCoordinator>();

await builder.Build().RunAsync();
