using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Auth;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.App.Tests.Auth;
using PSPad.Contracts;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SettingsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 3, 10);

    InMemoryLocalSessionStore? _sessions;
    InMemoryReplica? _replica;
    LocalAuthenticationStateProvider? _authProvider;

    [Fact]
    public void ItShowsTheAccountName()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Ada Lovelace", page.Markup);
        Assert.Contains("ada@example.com", page.Markup);
    }

    [Fact]
    public async Task ChoosingATimeZoneSendsItAndRefreshesToday()
    {
        var state = Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.ApplyTimeZoneAsync("Pacific/Kiritimati"));

        Assert.Equal("Pacific/Kiritimati", state.TimeZone);
        Assert.Equal(
            TodayRule.TodayIn(
                new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati")),
            state.Today);
    }

    [Fact]
    public async Task ANullTimeZoneFromTheServerDoesNotCrashTheSettingsPage()
    {
        var state = Arrange(displayName: "Ada", email: "ada@example.com", respondWithNullTimeZone: true);

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.ApplyTimeZoneAsync("Pacific/Kiritimati"));

        Assert.Equal("Etc/UTC", state.TimeZone);
    }

    [Fact]
    public async Task ATimeZoneTheBrowserCannotResolveFallsBackToUtcInsteadOfThrowing()
    {
        var state = Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.ApplyTimeZoneAsync("Mars/Olympus_Mons"));

        Assert.Equal("Mars/Olympus_Mons", state.TimeZone);
        Assert.Equal(Today, state.Today);
    }

    [Fact]
    public void ItShowsEverythingSyncedWhenTheOutboxIsEmpty()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Everything is synced.", page.Markup);
    }

    [Fact]
    public async Task ItShowsThePendingCommandCountFromTheOutboxRegardlessOfSyncCoordinatorState()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var outbox = Services.GetRequiredService<IOutbox>();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));

        var page = Render<SettingsPage>();

        Assert.Contains("2 pending", page.Markup);
    }

    [Fact]
    public async Task SigningOutClearsTheLocalSessionAndTheReplicaAndNotifiesTheStateProvider()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var notifications = 0;
        _authProvider!.AuthenticationStateChanged += _ => notifications++;

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.Null(_sessions!.Current);
        Assert.Null(await _replica!.OwnerAsync());
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task SigningOutKeepsTheOutbox()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var outbox = Services.GetRequiredService<IOutbox>();
        await outbox.AppendAsync(
            Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public void SigningOutClearsBeforeLeavingForKeycloakSoAnInterruptedSignOutStillSignsOut()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        bool? clearedWhenLeaving = null;
        navigation.LocationChanged += (_, _) => clearedWhenLeaving = _sessions!.Current is null;

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.True(clearedWhenLeaving);
        Assert.EndsWith("authentication/logout", navigation.Uri);
    }

    [Fact]
    public void SigningOutAnnouncesTheAnonymousStateOnlyOnceTheLogoutRouteIsReached()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var routeWhenAnnounced = new List<string>();
        _authProvider!.AuthenticationStateChanged += _ => routeWhenAnnounced.Add(navigation.Uri);

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.All(routeWhenAnnounced, route => Assert.EndsWith("authentication/logout", route));
        Assert.NotEmpty(routeWhenAnnounced);
    }

    [Fact]
    public void ItOffersTheThreeThemeModes()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("System", page.Markup);
        Assert.Contains("Light", page.Markup);
        Assert.Contains("Dark", page.Markup);
    }

    AppState Arrange(string displayName, string email, bool respondWithNullTimeZone = false)
    {
        _replica = AppTestHost.Arrange(this, User, Today);
        _sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, displayName, email, "UTC", "refresh-token", DateTimeOffset.UtcNow));
        _authProvider = new LocalAuthenticationStateProvider(_sessions);
        Services.AddSingleton<ILocalSessionStore>(_sessions);
        Services.AddSingleton(_authProvider);
        Services.AddSingleton(new LocalSignOut(_sessions, _replica, _authProvider));

        var state = new AppState
        {
            UserId = User,
            Today = Today,
            DisplayName = displayName,
            Email = email
        };
        Services.AddSingleton(state);

        Services.AddSingleton(new PSPadApiClient(
            new HttpClient(new EchoTimeZoneHandler(User, displayName, email, respondWithNullTimeZone))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        return state;
    }

    sealed class EchoTimeZoneHandler(Guid userId, string displayName, string email, bool respondWithNullTimeZone)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<SetTimeZoneRequest>(cancellationToken);
            var timeZone = respondWithNullTimeZone ? null! : body!.TimeZone;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new MeResponse(userId, displayName, email, timeZone))
            };
        }
    }
}
