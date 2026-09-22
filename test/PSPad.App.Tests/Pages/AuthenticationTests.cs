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
using PSPad.App.State.Replica;
using PSPad.App.Tests.Auth;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AuthenticationTests : Bunit.TestContext
{
    readonly InMemoryReplica _replica = new();

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
        Assert.Equal(1, notified);
    }

    [Fact]
    public void ItStoresTheRotatedRefreshTokenRatherThanTheCapturedOne()
    {
        var sessions = new InMemoryLocalSessionStore();
        Arrange(
            sessions,
            new StubMeHandler(
                HttpStatusCode.OK, new MeResponse(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw")),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: "refresh-token-value");

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Equal("rotated-refresh-token", sessions.Current?.RefreshToken);
    }

    [Fact]
    public void ItWritesNothingWhenTheTokenExchangeDoesNotRenew()
    {
        var sessions = new InMemoryLocalSessionStore();
        Arrange(
            sessions,
            new StubMeHandler(
                HttpStatusCode.OK, new MeResponse(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw")),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: "refresh-token-value",
            tokenEndpoint: new UnreachableTokenEndpoint());

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Null(sessions.Current);
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

    [Fact]
    public void ItWritesNothingWhenTheProfileCarriesNoUserId()
    {
        var sessions = new InMemoryLocalSessionStore();
        Arrange(
            sessions,
            new StubMeHandler(
                HttpStatusCode.OK, new MeResponse(Guid.Empty, "Zoe", "zoe@example.com", "Europe/Warsaw")),
            completeSignInStatus: RemoteAuthenticationStatus.Success,
            refreshToken: "refresh-token-value");

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Null(sessions.Current);
    }

    [Fact]
    public async Task ACompletedLogOutClearsTheLocalSessionAndTheReplica()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "r0", DateTimeOffset.UtcNow));
        Arrange(sessions, new StubMeHandler(HttpStatusCode.NotFound, null));
        JSInterop.Setup<RemoteAuthenticationResult<RemoteAuthenticationState>>(
                "AuthenticationService.completeSignOut", _ => true)
            .SetResult(new RemoteAuthenticationResult<RemoteAuthenticationState>
            {
                Status = RemoteAuthenticationStatus.Success,
                State = new RemoteAuthenticationState { ReturnUrl = "/" }
            });

        Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "logout-callback"));

        Assert.Null(sessions.Current);
        Assert.Null(await _replica.OwnerAsync());
    }

    LocalAuthenticationStateProvider Arrange(
        ILocalSessionStore sessions,
        HttpMessageHandler meHandler,
        RemoteAuthenticationStatus completeSignInStatus = RemoteAuthenticationStatus.OperationCompleted,
        string? refreshToken = null,
        HttpMessageHandler? tokenEndpoint = null)
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

        var clock = new FixedClock();
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        _replica.SetOwnerAsync(Guid.NewGuid()).GetAwaiter().GetResult();
        Services.AddSingleton<IReplica>(_replica);
        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(sessions);
        Services.AddSingleton(authProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authProvider);
        Services.AddSingleton(new LocalSignOut(sessions, _replica, authProvider));

        var refresher = new TokenRefresher(
            new HttpClient(tokenEndpoint ?? new RotatingTokenEndpoint()), clock,
            "http://localhost:8080/realms/pspad", "pspad-frontend");
        Services.AddSingleton(refresher);

        // The pipeline is the real one on purpose: a bare HttpClient over the /api/me stub would
        // carry an Authorization header no SessionAuthorizationHandler ever had to produce.
        var authorized = new SessionAuthorizationHandler(refresher, sessions, clock, authProvider)
        {
            InnerHandler = new BearerRequiredHandler(meHandler)
        };

        Services.AddSingleton(new PSPadApiClient(
            new HttpClient(authorized) { BaseAddress = new Uri("http://localhost/") }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://localhost:8080/realms/pspad",
                ["Keycloak:ClientId"] = "pspad-frontend"
            })
            .Build();
        Services.AddSingleton<IConfiguration>(configuration);

        return authProvider;
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

    sealed class BearerRequiredHandler(HttpMessageHandler authorized) : DelegatingHandler(authorized)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.Headers.Authorization is null
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))
                : base.SendAsync(request, cancellationToken);
    }

    sealed class RotatingTokenEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"at","expires_in":300,"refresh_token":"rotated-refresh-token"}""")
            });
    }

    sealed class UnreachableTokenEndpoint : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}
