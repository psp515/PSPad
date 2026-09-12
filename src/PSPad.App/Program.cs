using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App;
using PSPad.App.Api;
using PSPad.App.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("zoneinfo");
});

builder.Services.AddScoped<IReplica, InMemoryReplica>();
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

builder.Services.AddScoped<IHistorySource>(sp => sp.GetRequiredService<PSPadApiClient>());

await builder.Build().RunAsync();
