using System.Net;
using System.Net.Http.Json;
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
using PSPad.App.Tests.Auth;
using PSPad.Contracts;
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
        Arrange(
            new InMemoryLocalSessionStore(),
            new StubMeHandler(HttpStatusCode.NotFound, null));

        var page = Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Contains("pspad-boot-mark", page.Markup);
        Assert.DoesNotContain("Completing login", page.Markup);
    }

    [Fact]
    public void ItCapturesTheSessionAfterASuccessfulLogin()
    {
        var userId = Guid.NewGuid();
        var sessions = new InMemoryLocalSessionStore();
        var authProvider = Arrange(
            sessions,
            new StubMeHandler(HttpStatusCode.OK, new MeResponse(userId, "Zoe", "zoe@example.com", "Europe/Warsaw")),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: "refresh-token-value");

        var notified = 0;
        authProvider.AuthenticationStateChanged += _ => notified++;

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.NotNull(sessions.Current);
        Assert.Equal(userId, sessions.Current!.UserId);
        Assert.Equal("Zoe", sessions.Current.DisplayName);
        Assert.Equal("zoe@example.com", sessions.Current.Email);
        Assert.Equal("Europe/Warsaw", sessions.Current.TimeZone);
        Assert.Equal("refresh-token-value", sessions.Current.RefreshToken);
        Assert.Equal(1, notified);
    }

    [Fact]
    public void ItWritesNothingWhenNoRefreshTokenIsCaptured()
    {
        var sessions = new InMemoryLocalSessionStore();
        Arrange(
            sessions,
            new StubMeHandler(
                HttpStatusCode.OK, new MeResponse(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw")),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: null);

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Null(sessions.Current);
    }

    [Fact]
    public void ItWritesNothingWhenTheProfileFetchFails()
    {
        var sessions = new InMemoryLocalSessionStore();
        Arrange(
            sessions,
            new StubMeHandler(HttpStatusCode.InternalServerError, null),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: "refresh-token-value");

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Null(sessions.Current);
    }

    LocalAuthenticationStateProvider Arrange(
        ILocalSessionStore sessions,
        HttpMessageHandler meHandler,
        RemoteAuthenticationStatus completeSignInStatus = RemoteAuthenticationStatus.OperationCompleted,
        string? refreshToken = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.Options = new ServiceProviderOptions { ValidateScopes = false };
        Services.RemoveAll<AuthenticationStateProvider>();

        JSInterop.Setup<RemoteAuthenticationResult<RemoteAuthenticationState>>(
                "AuthenticationService.completeSignIn", _ => true)
            .SetResult(new RemoteAuthenticationResult<RemoteAuthenticationState>
            {
                Status = completeSignInStatus,
                State = new RemoteAuthenticationState { ReturnUrl = "/" }
            });

        JSInterop.Setup<string?>("captureOidcRefreshToken", _ => true).SetResult(refreshToken);

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
        Services.AddSingleton(sessions);
        Services.AddSingleton<LocalAuthenticationStateProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(
            services => services.GetRequiredService<LocalAuthenticationStateProvider>());
        Services.AddSingleton(new PSPadApiClient(
            new HttpClient(meHandler) { BaseAddress = new Uri("http://localhost/") }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://localhost:8080/realms/pspad",
                ["Keycloak:ClientId"] = "pspad-frontend"
            })
            .Build();
        Services.AddSingleton<IConfiguration>(configuration);

        return Services.GetRequiredService<LocalAuthenticationStateProvider>();
    }

    sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    }

    sealed class StubMeHandler(HttpStatusCode status, MeResponse? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body);
            }

            return Task.FromResult(response);
        }
    }
}
