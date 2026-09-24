using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Sync;
using PSPad.App.Auth;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.App.Tests.Auth;
using PSPad.App.Theme;
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
    public async Task SigningOutClearsTheLocalSessionAndTheReplica()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.Null(_sessions!.Current);
        Assert.Null(await _replica!.OwnerAsync());
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
        string? whereWeStillWere = null;

        var page = Render<SettingsPage>();
        _sessions!.Cleared = () => whereWeStillWere = navigation.Uri;
        page.Find(".pspad-sign-out").Click();

        Assert.Equal("http://localhost/", whereWeStillWere);
        Assert.StartsWith("http://localhost:8080/", navigation.Uri);
    }

    [Fact]
    public void SigningOutEndsTheKeycloakSessionAndComesBackToTheWelcomeScreen()
    {
        // Dropping only the local session leaves Keycloak's own alive, and the next authorization
        // bounce signs the same user straight back in without ever asking.
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.StartsWith(
            "http://localhost:8080/realms/psplace/protocol/openid-connect/logout?", navigation.Uri);
        Assert.Contains("client_id=pspad-frontend", navigation.Uri);
        Assert.Contains(
            $"post_logout_redirect_uri={Uri.EscapeDataString("http://localhost/welcome")}",
            navigation.Uri);
    }

    [Fact]
    public void SigningOutStaysLocalAndAnonymousWhenThereIsNoNetworkToReachKeycloakOn()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", online: false);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var routeWhenAnnounced = new List<string>();
        _authProvider!.AuthenticationStateChanged += _ => routeWhenAnnounced.Add(navigation.Uri);

        var page = Render<SettingsPage>();
        page.Find(".pspad-sign-out").Click();

        Assert.Equal("http://localhost/welcome", navigation.Uri);
        Assert.All(routeWhenAnnounced, route => Assert.EndsWith("/welcome", route));
        Assert.NotEmpty(routeWhenAnnounced);
    }

    [Fact]
    public void ItOffersTheThreeThemeModes()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        var items = page.FindComponents<MudSelectItem<ThemeMode>>();
        Assert.Equal(3, items.Count);
        var modes = items.Select(item => item.Instance.Value).ToList();
        Assert.Contains(ThemeMode.System, modes);
        Assert.Contains(ThemeMode.Light, modes);
        Assert.Contains(ThemeMode.Dark, modes);
    }

    [Fact]
    public async Task SelectingATimeZoneAppliesItThroughThePreference()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.SelectThemeAsync(ThemeMode.Dark));

        Assert.Equal(ThemeMode.Dark, Services.GetRequiredService<ThemePreference>().Mode);
    }

    [Fact]
    public void TheThemeControlIsASelectNotAButtonGroup()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Empty(page.FindAll(".mud-button-group"));
        Assert.NotEmpty(page.FindAll(".mud-select"));
    }

    [Fact]
    public void TheTimeZonePickerIsSearchableRatherThanAFlatDropdown()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".mud-autocomplete"));
    }

    [Fact]
    public async Task TypingNarrowsTheTimeZoneSearchResultsCaseInsensitively()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        var matches = await page.Instance.SearchTimeZonesAsync("warsaw", CancellationToken.None);

        Assert.Contains("Europe/Warsaw", matches);
        Assert.DoesNotContain("Pacific/Kiritimati", matches);
    }

    [Fact]
    public void TheCardsSitInAResponsiveGrid()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".mud-grid"));
    }

    [Fact]
    public void TheSignOutButtonLivesInsideTheAccountCard()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".pspad-account-card .pspad-sign-out"));
    }

    [Fact]
    public void TheDangerZoneCardOffersAccountDeletion()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".pspad-delete-account"));
        Assert.False(page.Find(".pspad-delete-account").HasAttribute("disabled"));
    }

    [Fact]
    public void TheDeleteAccountButtonIsDisabledWhenOffline()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", online: false);

        var page = Render<SettingsPage>();

        Assert.True(page.Find(".pspad-delete-account").HasAttribute("disabled"));
    }

    [Fact]
    public void TheDeleteConfirmButtonStaysDisabledUntilTheEmailMatches()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();

        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));

        page.Find(".pspad-confirm-email input").Input("someone-else@example.com");
        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));

        page.Find(".pspad-confirm-email input").Input("ADA@EXAMPLE.COM");
        Assert.False(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ConfirmingDeletesTheAccountClearsLocalStateAndReturnsToWelcome()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Null(_sessions!.Current);
        Assert.Null(await _replica!.OwnerAsync());
        Assert.Equal("http://localhost/welcome", navigation.Uri);
    }

    [Fact]
    public void AFailedDeleteShowsAnErrorAndKeepsTheDialogOpen()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", accountDeleteFails: true);

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        page.Find(".pspad-confirm-delete").Click();

        Assert.Contains("Something went wrong", page.Markup);
        Assert.NotEmpty(page.FindAll(".pspad-confirm-delete"));
    }

    RenderFragment BuildSettingsPageWithDialogs() => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<SettingsPage>(2);
        builder.CloseComponent();
    };

    AppState Arrange(
        string displayName, string email, bool respondWithNullTimeZone = false, bool online = true,
        bool accountDeleteFails = false)
    {
        _replica = AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<IConnectivity>(new FixedConnectivity(online));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://localhost:8080/realms/psplace",
                ["Keycloak:ClientId"] = "pspad-frontend"
            })
            .Build());
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
            new HttpClient(new StubApiHandler(User, displayName, email, respondWithNullTimeZone, accountDeleteFails))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        Services.AddSingleton<LocalAccountDeletion>(services => new LocalAccountDeletion(
            _sessions, _replica, services.GetRequiredService<IOutbox>(), _authProvider));

        return state;
    }

    sealed class FixedConnectivity(bool online) : IConnectivity
    {
        public bool IsOnline => online;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    sealed class StubApiHandler(
        Guid userId, string displayName, string email, bool respondWithNullTimeZone, bool accountDeleteFails)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Delete)
            {
                return accountDeleteFails
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"keycloakRemoved":true}""")
                    };
            }

            var body = await request.Content!.ReadFromJsonAsync<SetTimeZoneRequest>(cancellationToken);
            var timeZone = respondWithNullTimeZone ? null! : body!.TimeZone;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new MeResponse(userId, displayName, email, timeZone))
            };
        }
    }
}
