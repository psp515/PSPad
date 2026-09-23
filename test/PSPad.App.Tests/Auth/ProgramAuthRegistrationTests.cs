using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class ProgramAuthRegistrationTests
{
    [Fact]
    public async Task ItResolvesAuthenticationStateProviderToTheLocalImplementationOverTheOidcLibrarysOwn()
    {
        await using var services = BuildServices();

        var resolved = services.GetRequiredService<AuthenticationStateProvider>();

        Assert.IsType<LocalAuthenticationStateProvider>(resolved);
    }

    [Fact]
    public async Task ItSharesOneLocalAuthenticationStateProviderAcrossScopes()
    {
        await using var services = BuildServices();

        using var first = services.CreateScope();
        using var second = services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<LocalAuthenticationStateProvider>(),
            second.ServiceProvider.GetRequiredService<LocalAuthenticationStateProvider>());
    }

    [Fact]
    public async Task ItStillResolvesTheOidcLibrarysRemoteAuthenticationServiceDespiteTheLocalOverride()
    {
        await using var services = BuildServices();

        using var scope = services.CreateScope();

        var resolved = scope.ServiceProvider
            .GetRequiredService<IRemoteAuthenticationService<RemoteAuthenticationState>>();

        Assert.IsType<RemoteAuthenticationService<RemoteAuthenticationState, RemoteUserAccount, OidcProviderOptions>>(
            resolved);
    }

    [Fact]
    public async Task ItStillResolvesTheOidcLibrarysAccessTokenProviderDespiteTheLocalOverride()
    {
        await using var services = BuildServices();

        using var scope = services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IAccessTokenProvider>();

        Assert.IsType<RemoteAuthenticationService<RemoteAuthenticationState, RemoteUserAccount, OidcProviderOptions>>(
            resolved);
    }

    [Fact]
    public async Task ItSharesOneTokenRefresherAcrossScopes()
    {
        await using var services = BuildServices();

        using var first = services.CreateScope();
        using var second = services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<TokenRefresher>(),
            second.ServiceProvider.GetRequiredService<TokenRefresher>());
    }

    static ServiceProvider BuildServices()
    {
        var collection = new ServiceCollection();

        collection.AddOidcAuthentication(options =>
        {
            options.ProviderOptions.Authority = "http://localhost:8080/realms/pspad";
            options.ProviderOptions.ClientId = "pspad-frontend";
            options.ProviderOptions.ResponseType = "code";
            options.ProviderOptions.DefaultScopes.Add("email");
        });

        collection.AddSingleton<IJSRuntime, ThrowingJSRuntime>();
        collection.AddSingleton<NavigationManager, FakeNavigationManager>();
        collection.AddSingleton<IClock, BrowserClock>();
        collection.AddSingleton<ILocalSessionStore, LocalSessionStore>();
        collection.AddSingleton(services => new TokenRefresher(
            new HttpClient(), services.GetRequiredService<IClock>(),
            "http://localhost:8080/realms/pspad", "pspad-frontend"));

        collection.AddScoped<IRemoteAuthenticationService<RemoteAuthenticationState>>(services =>
            services.GetServices<AuthenticationStateProvider>()
                .OfType<IRemoteAuthenticationService<RemoteAuthenticationState>>()
                .Single());

        collection.AddScoped<IAccessTokenProvider>(services =>
            services.GetServices<AuthenticationStateProvider>()
                .OfType<IAccessTokenProvider>()
                .Single());

        collection.AddSingleton<LocalAuthenticationStateProvider>();
        collection.AddSingleton<AuthenticationStateProvider>(
            services => services.GetRequiredService<LocalAuthenticationStateProvider>());
        collection.AddScoped<SessionAuthorizationHandler>();

        return collection.BuildServiceProvider(validateScopes: true);
    }

    sealed class FakeNavigationManager : NavigationManager
    {
        public FakeNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }

    sealed class ThrowingJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
    }
}
