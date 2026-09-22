using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.Layout;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AuthenticationTests : Bunit.TestContext
{
    [Fact]
    public void TheLoginCallbackRouteNeverMountsAppShell()
    {
        // AppShell's account load is a one-shot check of the cascading auth state, run once
        // at OnInitializedAsync. On this route that check races RemoteAuthenticatorView's own
        // processing of the OIDC redirect and reliably sees "unauthenticated" first, locking
        // AppShell into an anonymous session (Guid.Empty) for the rest of the SPA's lifetime --
        // the client-side navigation RemoteAuthenticatorView does afterwards never remounts the
        // layout to re-check. Every command sent from then on embeds the wrong user id and the
        // server rejects it forever as "for a different user." Giving this route its own layout
        // keeps AppShell from ever seeing the mid-login state.
        var layout = typeof(Authentication).GetCustomAttribute<LayoutAttribute>();

        Assert.NotNull(layout);
        Assert.Equal(typeof(AuthenticationLayout), layout!.LayoutType);
        Assert.NotEqual(typeof(AppShell), layout.LayoutType);
    }

    [Fact]
    public void ItShowsTheBrandMarkInsteadOfTheLibraryText()
    {
        Arrange();

        var page = Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Contains("pspad-boot-mark", page.Markup);
        Assert.DoesNotContain("Completing login", page.Markup);
    }

    void Arrange()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.Options = new ServiceProviderOptions { ValidateScopes = false };
        Services.RemoveAll<AuthenticationStateProvider>();

        JSInterop.Setup<RemoteAuthenticationResult<RemoteAuthenticationState>>(
                "AuthenticationService.completeSignIn", _ => true)
            .SetResult(new RemoteAuthenticationResult<RemoteAuthenticationState>
            {
                Status = RemoteAuthenticationStatus.OperationCompleted
            });

        Services.AddOidcAuthentication(options =>
        {
            options.ProviderOptions.Authority = "http://localhost:8080/realms/pspad";
            options.ProviderOptions.ClientId = "pspad-frontend";
            options.ProviderOptions.ResponseType = "code";
            options.ProviderOptions.DefaultScopes.Add("email");
        });

        Services.AddScoped<IRemoteAuthenticationService<RemoteAuthenticationState>>(services =>
            services.GetServices<AuthenticationStateProvider>()
                .OfType<IRemoteAuthenticationService<RemoteAuthenticationState>>()
                .Single());

        Services.AddSingleton<IClock>(new FixedClock());
        Services.AddSingleton<ILocalSessionStore>(new FakeLocalSessionStore());
        Services.AddSingleton<LocalAuthenticationStateProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(
            services => services.GetRequiredService<LocalAuthenticationStateProvider>());
        Services.AddSingleton(new PSPadApiClient(new HttpClient()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://localhost:8080/realms/pspad",
                ["Keycloak:ClientId"] = "pspad-frontend"
            })
            .Build();
        Services.AddSingleton<IConfiguration>(configuration);
    }

    sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    sealed class FakeLocalSessionStore : ILocalSessionStore
    {
        public Task<LocalSession?> LoadAsync() => Task.FromResult<LocalSession?>(null);

        public Task SaveAsync(LocalSession session) => Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }
}
