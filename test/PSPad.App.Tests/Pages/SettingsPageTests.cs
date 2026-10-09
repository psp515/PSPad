using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Sync;
using PSPad.App.Auth;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Viewport;
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
    public void ItNamesItself()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var page = Render<SettingsPage>();

        page.WaitForAssertion(() =>
            Assert.Equal("Settings", Services.GetRequiredService<PageHeader>().Title));
    }

    [Fact]
    public void ItsHeaderCarriesAnIconTileAndASubtitleLikeTheAreaScreen()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var page = Render<SettingsPage>();

        page.WaitForAssertion(() =>
        {
            var tile = page.Find(".pspad-page-heading .pspad-page-icon");
            Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.Settings), tile.InnerHtml);
            Assert.Equal("Account, appearance and sync", page.Find(".pspad-page-heading .pspad-page-subtitle").TextContent.Trim());
            var header = Services.GetRequiredService<PageHeader>();
            Assert.Equal(MudBlazor.Icons.Material.Outlined.Settings, header.Icon);
            Assert.Equal("Account, appearance and sync", header.Subtitle);
        });
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
    public void TheSyncSectionSaysWhenItLastSynced()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger { LastSyncedAt = new DateTimeOffset(2026, 3, 9, 23, 58, 0, TimeSpan.Zero) };
        Services.AddSingleton<ISyncStatus>(sync);

        var page = Render<SettingsPage>();

        Assert.Contains("Last synced 2 min ago", page.Markup);
    }

    [Fact]
    public void TheSyncSectionSaysWhenNothingHasSyncedYet()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Not synced yet", page.Markup);
    }

    [Fact]
    public void TheSyncSectionHasASyncNowButtonThatRunsOneSync()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger();
        Services.AddSingleton<ISyncTrigger>(sync);
        Services.AddSingleton<ISyncStatus>(sync);

        var page = Render<SettingsPage>();
        page.Find(".pspad-sync-button").Click();

        Assert.Equal(1, sync.Calls);
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
    public void AFailedSyncIsSaidSoInsteadOfEverythingIsSynced()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger { LastSyncFailed = true };
        Services.AddSingleton<ISyncStatus>(sync);

        var page = Render<SettingsPage>();

        Assert.Contains("Couldn't sync.", page.Markup);
        Assert.DoesNotContain("Everything is synced.", page.Markup);
    }

    [Fact]
    public void TheSyncSectionFollowsASyncFromRunningToFailedToDone()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger();
        Services.AddSingleton<ISyncStatus>(sync);
        var page = Render<SettingsPage>();
        Assert.Contains("Everything is synced.", page.Markup);

        sync.Hold();
        _ = sync.SyncNowAsync();
        page.WaitForAssertion(() => Assert.Contains("Syncing…", page.Markup));

        sync.LastSyncFailed = true;
        sync.Release();
        page.WaitForAssertion(() => Assert.Contains("Couldn't sync.", page.Markup));

        sync.LastSyncFailed = false;
        sync.Announce();
        page.WaitForAssertion(() => Assert.Contains("Everything is synced.", page.Markup));
    }

    [Fact]
    public async Task ThePendingCountDropsWhenASyncDrainsTheOutbox()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger();
        Services.AddSingleton<ISyncStatus>(sync);
        var outbox = Services.GetRequiredService<IOutbox>();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        var page = Render<SettingsPage>();
        Assert.Contains("1 pending", page.Markup);

        var batch = await outbox.PeekAsync(10);
        await outbox.RemoveThroughAsync(batch[^1].Position);
        sync.Announce();

        page.WaitForAssertion(() => Assert.Contains("Everything is synced.", page.Markup));
    }

    [Fact]
    public void TheSyncCardIsTheDetailsAnchor()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Sync", page.Find("#sync").TextContent);
    }

    [Fact]
    public void TheSyncCardListsTheChangesTheLastSyncCouldNotSave()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger { LastRejections = ["That list no longer exists.", "Too late."] };
        Services.AddSingleton<ISyncStatus>(sync);

        var page = Render<SettingsPage>();

        var rejected = page.FindAll("#sync .pspad-sync-rejection").Select(item => item.TextContent.Trim());
        Assert.Equal(["That list no longer exists.", "Too late."], rejected);
    }

    [Fact]
    public void WithNothingRejectedTheSyncCardListsNothing()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Empty(page.FindAll(".pspad-sync-rejections"));
    }

    [Fact]
    public void RejectionsArrivingLaterShowUp()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger();
        Services.AddSingleton<ISyncStatus>(sync);
        var page = Render<SettingsPage>();

        sync.LastRejections = ["That list no longer exists."];
        sync.Announce();

        page.WaitForAssertion(() =>
            Assert.Equal("That list no longer exists.", page.Find(".pspad-sync-rejection").TextContent.Trim()));
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
    public async Task SelectingAThemeAppliesItThroughThePreference()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.SelectThemeAsync(ThemeMode.Dark));

        Assert.Equal(ThemeMode.Dark, Services.GetRequiredService<ThemePreference>().Mode);
    }

    [Fact]
    public async Task SwitchingTheThemeRetagsTheDocumentSoTheCardTokensFollow()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.SelectThemeAsync(ThemeMode.Dark));
        var afterDark = LastDocumentTheme();
        await page.InvokeAsync(() => page.Instance.SelectThemeAsync(ThemeMode.Light));

        Assert.Equal("dark", afterDark);
        Assert.Equal("light", LastDocumentTheme());
    }

    string? LastDocumentTheme() =>
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "document.documentElement.setAttribute")
            .Select(invocation => invocation.Arguments[1] as string)
            .LastOrDefault();

    [Fact]
    public void TimeZoneThemeAndAccentShareOneApplicationSettingsCard()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        var card = page.Find(".pspad-application-settings");
        Assert.Contains("Application settings", card.TextContent);
        Assert.NotNull(card.QuerySelector(".mud-autocomplete"));
        Assert.NotNull(card.QuerySelector(".mud-select"));
        Assert.NotNull(card.QuerySelector(".pspad-accent-picker"));
    }

    [Fact]
    public void ThemeAndTimeZoneAreLabelled()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Equal("Theme", page.FindComponent<MudSelect<ThemeMode>>().Instance.Label);
        Assert.Equal("Time zone", page.FindComponent<MudAutocomplete<string>>().Instance.Label);
    }

    [Fact]
    public void ItOffersASwatchPerPresetAndOneForACustomColour()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Equal(PSPadTheme.Presets.Count, page.FindAll(".pspad-accent-preset").Count);
        Assert.Single(page.FindAll(".pspad-accent-custom"));
    }

    [Fact]
    public void ClickingASwatchAppliesThatAccent()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        page.Find("[aria-label='Purple accent']").Click();

        Assert.Equal(Accent.Purple, Services.GetRequiredService<ThemePreference>().Accent);
        page.WaitForAssertion(() =>
            Assert.Equal("true", page.Find("[aria-label='Purple accent']").GetAttribute("aria-pressed")));
        Assert.Equal("false", page.Find("[aria-label='Green accent']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void TheEditSwatchOpensAColourPicker()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-accent-custom").Click();

        Assert.NotEmpty(page.FindComponents<MudColorPicker>());
        Assert.Empty(page.FindAll(".mud-dialog-fullscreen"));
    }

    [Fact]
    public void OnAPhoneTheColourPickerFillsTheScreen()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-accent-custom").Click();

        Assert.NotEmpty(page.FindAll(".mud-dialog-fullscreen"));
    }

    [Fact]
    public async Task ApplyingAPickedColourMakesItTheAccent()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-accent-custom").Click();
        var dialog = page.FindComponent<AccentColorDialog>();
        await dialog.InvokeAsync(() => dialog.Instance.Pick("#ff00aa"));
        page.Find(".pspad-accent-apply").Click();

        var preference = Services.GetRequiredService<ThemePreference>();
        page.WaitForAssertion(() => Assert.Equal(Accent.Custom, preference.Accent));
        Assert.Equal("#ff00aa", preference.CustomColor);
    }

    [Fact]
    public void CancellingThePickerKeepsTheAccent()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-accent-custom").Click();
        page.Find(".pspad-accent-cancel").Click();

        Assert.Equal(Accent.Green, Services.GetRequiredService<ThemePreference>().Accent);
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
    public void TheAccountCardLinksToKeycloaksAccountConsoleInANewTabForChangingThePassword()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        var link = page.Find(".pspad-account-card a.pspad-change-password");

        Assert.Equal("http://localhost:8080/realms/psplace/account/", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("Change password", link.TextContent);
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

    [Fact]
    public async Task WhenKeycloakRemovalFailsTheDialogNamesAnAdministratorBeforeFinishingTheDeletion()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", keycloakRemoved: false);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Contains("administrator", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("http://localhost/welcome", navigation.Uri);
        Assert.NotNull(_sessions!.Current);

        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Null(_sessions!.Current);
        Assert.Equal("http://localhost/welcome", navigation.Uri);
    }

    [Fact]
    public void TheDeleteConfirmButtonStaysDisabledWhenTheAccountHasNoEmailToConfirmAgainst()
    {
        Arrange(displayName: "Ada", email: "");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();

        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));
    }

    [Fact]
    public async Task DeletingClearsStorageBeforeLeavingForWelcomeAndAnnouncesSignedOutOnlyAfter()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        string? whereWeStillWere = null;
        var routeWhenAnnounced = new List<string>();
        _authProvider!.AuthenticationStateChanged += _ => routeWhenAnnounced.Add(navigation.Uri);

        var page = Render(BuildSettingsPageWithDialogs());
        _sessions!.Cleared = () => whereWeStillWere = navigation.Uri;
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Equal("http://localhost/", whereWeStillWere);
        Assert.NotEmpty(routeWhenAnnounced);
        Assert.All(routeWhenAnnounced, route => Assert.EndsWith("/welcome", route));
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
        bool accountDeleteFails = false, bool keycloakRemoved = true)
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
        Services.AddSingleton(new LocalSignOut(_sessions, _replica, _authProvider, NullLogger<LocalSignOut>.Instance));

        var state = new AppState
        {
            UserId = User,
            Today = Today,
            DisplayName = displayName,
            Email = email
        };
        Services.AddSingleton(state);

        Services.AddSingleton(new PSPadApiClient(
            new HttpClient(new StubApiHandler(
                User, displayName, email, respondWithNullTimeZone, accountDeleteFails, keycloakRemoved))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        Services.AddSingleton<LocalAccountDeletion>(services => new LocalAccountDeletion(
            _sessions, _replica, services.GetRequiredService<IOutbox>(), _authProvider,
            NullLogger<LocalAccountDeletion>.Instance));

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
        Guid userId, string displayName, string email, bool respondWithNullTimeZone, bool accountDeleteFails,
        bool keycloakRemoved = true)
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
                        Content = new StringContent(
                            $$"""{"keycloakRemoved":{{(keycloakRemoved ? "true" : "false")}}}""")
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
