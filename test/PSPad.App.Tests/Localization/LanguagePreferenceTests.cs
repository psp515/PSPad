using System.Globalization;
using PSPad.App.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
[Collection("Culture")]
public class LanguagePreferenceTests
{
    static async Task WithRestoredCultureAsync(Func<Task> body)
    {
        var before = CultureInfo.CurrentUICulture;
        var beforeDefault = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            await body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = before;
            CultureInfo.DefaultThreadCurrentUICulture = beforeDefault;
            CultureInfo.DefaultThreadCurrentCulture = beforeDefault;
        }
    }

    [Fact]
    public Task WithNothingStoredItFollowsTheBrowser() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "pl-PL" };
        var preference = new LanguagePreference(js);

        await preference.InitialiseAsync();

        Assert.Equal("pl", preference.Language);
        Assert.Equal("pl", CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName);
    });

    [Fact]
    public Task AnUnsupportedBrowserLanguageFallsBackToEnglish() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "de-DE" };
        var preference = new LanguagePreference(js);

        await preference.InitialiseAsync();

        Assert.Equal("en", preference.Language);
    });

    [Fact]
    public Task AStoredLanguageWinsOverTheBrowser() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "pl-PL" };
        js.Storage["pspad.language"] = "en";
        var preference = new LanguagePreference(js);

        await preference.InitialiseAsync();

        Assert.Equal("en", preference.Language);
    });

    [Fact]
    public Task TheServerValueWinsOverTheCacheWhenNothingIsPending() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "en" };
        var preference = new LanguagePreference(js);
        await preference.InitialiseAsync();

        var changed = await preference.AdoptFromServerAsync("pl");

        Assert.True(changed);
        Assert.Equal("pl", preference.Language);
        Assert.Equal("pl", js.Storage["pspad.language"]);
    });

    [Fact]
    public Task APendingChoiceIsNotOverwrittenByTheServer() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "en" };
        var preference = new LanguagePreference(js);
        await preference.InitialiseAsync();
        await preference.ChooseAsync("pl");

        var changed = await preference.AdoptFromServerAsync("en");

        Assert.False(changed);
        Assert.Equal("pl", preference.Language);
        Assert.True(preference.HasPendingWrite);
    });

    [Fact]
    public Task ClearingThePendingFlagRemovesIt() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "en" };
        var preference = new LanguagePreference(js);
        await preference.InitialiseAsync();
        await preference.ChooseAsync("pl");

        await preference.ClearPendingAsync();

        Assert.False(preference.HasPendingWrite);
        Assert.False(js.Storage.ContainsKey("pspad.language.pending"));
    });

    [Fact]
    public Task TheSameServerValueChangesNothing() => WithRestoredCultureAsync(async () =>
    {
        var js = new FakeStorageJsRuntime { BrowserLanguage = "pl" };
        var preference = new LanguagePreference(js);
        await preference.InitialiseAsync();

        Assert.False(await preference.AdoptFromServerAsync("pl"));
    });
}
