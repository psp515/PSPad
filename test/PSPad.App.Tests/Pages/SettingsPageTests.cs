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
using PSPad.App.Localization;
using PSPad.App.Statistics;
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
using PSPad.Module.Money.Preferences;
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
    RevocationEndpoint? _revocation;
    readonly List<HttpRequestMessage> _requests = [];

    [Fact]
    public async Task ChoosingALanguageCachesItSendsItAndReloads()
    {
        var state = Arrange(displayName: "Ada", email: "ada@example.com");
        var page = Render<SettingsPage>();

        await page.InvokeAsync(() => page.Instance.ApplyLanguageAsync("pl"));

        var preference = Services.GetRequiredService<LanguagePreference>();
        Assert.Equal("pl", preference.Language);
        Assert.False(preference.HasPendingWrite);
        Assert.Equal("pl", state.Language);
        Assert.Contains("/api/me/language", _requests.Select(request => request.RequestUri!.PathAndQuery));
        Assert.True(ReloadRequested);
    }

    [Fact]
    public async Task WhenTheServerIsUnreachableTheChoiceStaysPending()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", serverDown: true);
        var page = Render<SettingsPage>();

        await page.InvokeAsync(() => page.Instance.ApplyLanguageAsync("pl"));

        var preference = Services.GetRequiredService<LanguagePreference>();
        Assert.Equal("pl", preference.Language);
        Assert.True(preference.HasPendingWrite);
        Assert.True(ReloadRequested);
    }

    [Fact]
    public async Task TheLanguageSelectListsEveryLanguageByNativeName()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var popovers = Render<MudPopoverProvider>();
        var page = Render<SettingsPage>();

        var select = page.FindComponents<MudSelect<string>>().Single(c => c.Instance.Class!.Contains("pspad-language"));
        await page.InvokeAsync(() => select.Instance.OpenMenu());

        popovers.WaitForAssertion(() =>
        {
            Assert.Contains("English", popovers.Markup);
            Assert.Contains("Polski", popovers.Markup);
        });
    }

    bool ReloadRequested =>
        Services.GetRequiredService<NavigationManager>() is Bunit.TestDoubles.BunitNavigationManager navigation
        && navigation.History.Any(entry => entry.Options.ForceLoad);

    [Fact]
    public void ItShowsTheAccountName()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Contains("Ada Lovelace", page.Markup);
        Assert.Contains("ada@example.com", page.Markup);
    }

    [Fact]
    public void TheDefaultCurrencyShowsPlnWithoutPreferences()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");
        var page = Render<SettingsPage>();

        var select = page.FindComponents<MudSelect<string>>().Single(c => c.Instance.Class!.Contains("pspad-default-currency"));
        Assert.Equal("PLN", select.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task PickingACurrencySendsSetDefaultCurrency()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");
        var page = Render<SettingsPage>();

        var select = page.FindComponents<MudSelect<string>>().Single(c => c.Instance.Class!.Contains("pspad-default-currency"));
        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync("EUR"));

        var entry = Assert.Single(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Equal(nameof(SetDefaultCurrency), entry.Envelope.Type);
        Assert.Equal("EUR", entry.Envelope.Payload.GetProperty("Currency").GetString());
    }

    [Fact]
    public async Task ARejectedCurrencyLeavesTheSelectOnTheOldValue()
    {
        Arrange(displayName: "Ada Lovelace", email: "ada@example.com");
        var page = Render<SettingsPage>();

        var select = page.FindComponents<MudSelect<string>>().Single(c => c.Instance.Class!.Contains("pspad-default-currency"));
        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync("XXX"));

        Assert.Equal("PLN", page.Find(".pspad-default-currency input").GetAttribute("value"));
        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
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
    public void AChangeHoldingUpTheQueueCanBeDiscardedFromTheSyncCard()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var sync = new GatedSyncTrigger { LastRejections = ["That budget does not exist."], HasStuckChange = true };
        Services.AddSingleton<ISyncStatus>(sync);
        var page = Render<SettingsPage>();

        page.Find("#sync .pspad-discard-stuck").Click();

        Assert.Equal(1, sync.Discards);
    }

    [Fact]
    public void ARejectionAlreadyDroppedOffersNoDiscard()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        Services.AddSingleton<ISyncStatus>(new GatedSyncTrigger { LastRejections = ["That budget does not exist."] });

        var page = Render<SettingsPage>();

        Assert.Empty(page.FindAll(".pspad-discard-stuck"));
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
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.Null(_sessions!.Current);
        Assert.Null(await _replica!.OwnerAsync());
    }

    [Fact]
    public async Task SigningOutRevokesTheRefreshTokenAtKeycloak()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.Equal(1, _revocation!.Requests);
    }

    [Fact]
    public async Task UnsyncedChangesAskForConfirmationBeforeAnythingIsRemoved()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var outbox = await QueueAPendingCommandAsync();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.Contains("1 unsynced change", page.Find(".mud-dialog").TextContent);
        Assert.Equal(1, await outbox.CountAsync());
        Assert.NotNull(_sessions!.Current);
        Assert.Equal(0, _revocation!.Requests);
        Assert.Equal("http://localhost/", navigation.Uri);
    }

    [Fact]
    public async Task ConfirmingRemovesTheUnsyncedChangesAndSignsOut()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var outbox = await QueueAPendingCommandAsync();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());
        await page.InvokeAsync(() => DialogButton(page, "Sign out").Click());

        page.WaitForAssertion(() => Assert.StartsWith("http://localhost:8080/", navigation.Uri));
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Null(_sessions!.Current);
    }

    [Fact]
    public async Task CancellingKeepsTheUnsyncedChangesAndTheSession()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var outbox = await QueueAPendingCommandAsync();
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());
        await page.InvokeAsync(() => DialogButton(page, "Cancel").Click());

        Assert.Equal(1, await outbox.CountAsync());
        Assert.NotNull(_sessions!.Current);
        Assert.Equal(0, _revocation!.Requests);
        Assert.Equal("http://localhost/", navigation.Uri);
    }

    [Fact]
    public async Task SigningOutClearsBeforeLeavingForKeycloakSoAnInterruptedSignOutStillSignsOut()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        string? whereWeStillWere = null;

        var page = Render<SettingsPage>();
        _sessions!.Cleared = () => whereWeStillWere = navigation.Uri;
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.Equal("http://localhost/", whereWeStillWere);
        Assert.StartsWith("http://localhost:8080/", navigation.Uri);
    }

    [Fact]
    public async Task SigningOutEndsTheKeycloakSessionItNamesAndComesBackToTheWelcomeScreen()
    {
        // Dropping only the local session leaves Keycloak's own alive, and the next authorization
        // bounce signs the same user straight back in without ever asking.
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.StartsWith(
            "http://localhost:8080/realms/psplace/protocol/openid-connect/logout?", navigation.Uri);
        Assert.Contains("client_id=pspad-frontend", navigation.Uri);
        Assert.Contains("id_token_hint=id-token", navigation.Uri);
        Assert.Contains(
            $"post_logout_redirect_uri={Uri.EscapeDataString("http://localhost/welcome")}",
            navigation.Uri);
    }

    [Fact]
    public void OfflineTheSignOutButtonIsDisabledAndSaysWhy()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", online: false);

        var page = Render<SettingsPage>();

        Assert.True(page.Find(".pspad-sign-out").HasAttribute("disabled"));
        Assert.Contains("can't be signed out securely", page.Find(".pspad-sign-out-offline").TextContent);
        Assert.NotNull(_sessions!.Current);
    }

    [Fact]
    public void TheSignOutButtonComesBackWithTheConnection()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var connectivity = new SwitchableConnectivity();
        Services.AddSingleton<IConnectivity>(connectivity);

        var page = Render<SettingsPage>();
        connectivity.GoOnline();

        page.WaitForAssertion(() => Assert.False(page.Find(".pspad-sign-out").HasAttribute("disabled")));
        Assert.Empty(page.FindAll(".pspad-sign-out-offline"));
    }

    [Fact]
    public async Task AnUnreachableKeycloakKeepsTheUserSignedInAndSaysSo()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", keycloakReachable: false);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var announced = false;
        _authProvider!.AuthenticationStateChanged += _ => announced = true;

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Find(".pspad-sign-out").Click());

        Assert.NotNull(_sessions!.Current);
        Assert.Equal(User, await _replica!.OwnerAsync());
        Assert.False(announced);
        Assert.Equal("http://localhost/", navigation.Uri);
        Assert.Contains(
            Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message?.Contains("still signed in") == true);
    }

    async Task<IOutbox> QueueAPendingCommandAsync()
    {
        var outbox = Services.GetRequiredService<IOutbox>();
        await outbox.AppendAsync(
            Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        return outbox;
    }

    static AngleSharp.Dom.IElement DialogButton(IRenderedComponent<IComponent> page, string text) =>
        page.FindAll(".mud-dialog button").Single(button => button.TextContent.Trim() == text);

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

    [Fact]
    public async Task AStaleSignInAsksToSignInAgainAndKeepsTheAccount()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", accountDeleteNeedsFreshSignIn: true);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Contains("sign in again", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(page.FindAll(".pspad-reauthenticate"));
        Assert.NotNull(_sessions!.Current);
        Assert.Equal("http://localhost/", navigation.Uri);
    }

    [Fact]
    public async Task SigningInAgainForcesAFreshKeycloakLoginThatComesBackToTheDeletion()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", accountDeleteNeedsFreshSignIn: true);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());
        page.Find(".pspad-reauthenticate").Click();

        Assert.Equal("http://localhost/authentication/login", navigation.Uri);
        using var state = JsonDocument.Parse(navigation.History.First().Options.HistoryEntryState!);
        var request = state.RootElement;
        Assert.Equal("http://localhost/settings?deleteAccount=confirm",
            Property(request, "returnUrl").GetString());
        var parameters = Property(request, "additionalRequestParameters");
        Assert.Equal("login", Property(parameters, "prompt").GetString());
        Assert.Equal("0", Property(parameters, "max_age").GetString());
        Assert.NotNull(_sessions!.Current);
    }

    [Fact]
    public void ComingBackFromSigningInAgainReopensTheDeleteDialog()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("settings?deleteAccount=confirm");

        var page = Render(BuildSettingsPageWithDialogs());

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".pspad-confirm-email input")));
        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));
        Assert.Equal("http://localhost/settings", navigation.Uri);
    }

    static JsonElement Property(JsonElement element, string name) =>
        element.EnumerateObject().Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;

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
        bool accountDeleteFails = false, bool keycloakRemoved = true, bool accountDeleteNeedsFreshSignIn = false,
        bool keycloakReachable = true, bool serverDown = false)
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
            new LocalSession(User, displayName, email, "UTC", "en", "refresh-token", DateTimeOffset.UtcNow, "id-token"));
        _authProvider = new LocalAuthenticationStateProvider(_sessions);
        Services.AddSingleton<ILocalSessionStore>(_sessions);
        Services.AddSingleton(_authProvider);
        _revocation = new RevocationEndpoint(keycloakReachable);
        Services.AddSingleton(services => new LocalSignOut(
            _sessions, _replica, services.GetRequiredService<IOutbox>(),
            services.GetRequiredService<StatisticsCache>(), services.GetRequiredService<ISnapshotCache>(),
            new KeycloakTokenRevoker(new HttpClient(_revocation), "http://localhost:8080/realms/psplace", "pspad-frontend"),
            _authProvider, NullLogger<LocalSignOut>.Instance));

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
                User, displayName, email, respondWithNullTimeZone, accountDeleteFails, keycloakRemoved,
                accountDeleteNeedsFreshSignIn, _requests, serverDown))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        Services.AddSingleton<LocalAccountDeletion>(services => new LocalAccountDeletion(
            _sessions, _replica, services.GetRequiredService<IOutbox>(), _authProvider,
            NullLogger<LocalAccountDeletion>.Instance));

        return state;
    }

    sealed class RevocationEndpoint(bool reachable) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return reachable
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                : throw new HttpRequestException("Keycloak is down.");
        }
    }

    sealed class SwitchableConnectivity : IConnectivity
    {
        public bool IsOnline { get; private set; }

        public event Action? CameOnline;

        public event Action? Changed;

        public void GoOnline()
        {
            IsOnline = true;
            Changed?.Invoke();
            CameOnline?.Invoke();
        }
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
        bool keycloakRemoved = true, bool accountDeleteNeedsFreshSignIn = false,
        List<HttpRequestMessage>? requests = null, bool serverDown = false)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests?.Add(request);

            if (request.RequestUri!.AbsolutePath == "/api/me/language")
            {
                if (serverDown)
                {
                    throw new HttpRequestException("Server is down.");
                }

                var language = await request.Content!.ReadFromJsonAsync<SetLanguageRequest>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeResponse(userId, displayName, email, "UTC", language!.Language))
                };
            }

            if (request.Method == HttpMethod.Delete && accountDeleteNeedsFreshSignIn)
            {
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                challenge.Headers.TryAddWithoutValidation(
                    "WWW-Authenticate", """Bearer error="insufficient_user_authentication", max_age=300""");
                return challenge;
            }

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
                Content = JsonContent.Create(new MeResponse(userId, displayName, email, timeZone, "en"))
            };
        }
    }
}
