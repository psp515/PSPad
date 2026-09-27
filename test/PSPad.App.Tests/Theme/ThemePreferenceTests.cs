using Microsoft.JSInterop;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class ThemePreferenceTests
{
    sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Storage { get; } = [];

        public string? Stored
        {
            get => Storage.GetValueOrDefault("pspad.theme");
            set => Storage["pspad.theme"] = value!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;

            if (identifier == "localStorage.setItem")
            {
                Storage[key] = (string)args[1]!;
            }

            return ValueTask.FromResult((TValue)(object)Storage.GetValueOrDefault(key, "")!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    [Fact]
    public async Task WithNothingStoredItFollowsTheSystem()
    {
        var preference = new ThemePreference(new FakeJsRuntime());

        await preference.InitialiseAsync(systemPrefersDark: true);

        Assert.Equal(ThemeMode.System, preference.Mode);
        Assert.True(preference.IsDark);
    }

    [Fact]
    public async Task SystemModeFollowsALightSystemToo()
    {
        var preference = new ThemePreference(new FakeJsRuntime());

        await preference.InitialiseAsync(systemPrefersDark: false);

        Assert.False(preference.IsDark);
    }

    [Fact]
    public async Task AStoredModeWinsOverTheSystem()
    {
        var js = new FakeJsRuntime { Stored = nameof(ThemeMode.Light) };
        var preference = new ThemePreference(js);

        await preference.InitialiseAsync(systemPrefersDark: true);

        Assert.Equal(ThemeMode.Light, preference.Mode);
        Assert.False(preference.IsDark);
    }

    [Theory]
    [InlineData(ThemeMode.Light, false)]
    [InlineData(ThemeMode.Dark, true)]
    public async Task SettingAModePinsItRegardlessOfTheSystem(ThemeMode mode, bool expectedDark)
    {
        var preference = new ThemePreference(new FakeJsRuntime());
        await preference.InitialiseAsync(systemPrefersDark: !expectedDark);

        await preference.SetAsync(mode);

        Assert.Equal(mode, preference.Mode);
        Assert.Equal(expectedDark, preference.IsDark);
    }

    [Fact]
    public async Task SettingSystemGoesBackToFollowingTheSystem()
    {
        var preference = new ThemePreference(new FakeJsRuntime());
        await preference.InitialiseAsync(systemPrefersDark: true);
        await preference.SetAsync(ThemeMode.Light);

        await preference.SetAsync(ThemeMode.System);

        Assert.Equal(ThemeMode.System, preference.Mode);
        Assert.True(preference.IsDark);
    }

    [Fact]
    public async Task SettingPersistsTheChoiceAndRaisesChanged()
    {
        var js = new FakeJsRuntime();
        var preference = new ThemePreference(js);
        await preference.InitialiseAsync(systemPrefersDark: false);
        var raised = 0;
        preference.Changed += () => raised++;

        await preference.SetAsync(ThemeMode.Dark);

        Assert.Equal(nameof(ThemeMode.Dark), js.Stored);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task WithNoAccentStoredItIsGreen()
    {
        var preference = new ThemePreference(new FakeJsRuntime());

        await preference.InitialiseAsync(systemPrefersDark: false);

        Assert.Equal(Accent.Green, preference.Accent);
    }

    [Fact]
    public async Task AStoredAccentIsRestored()
    {
        var js = new FakeJsRuntime();
        js.Storage["pspad.accent"] = nameof(Accent.Purple);
        var preference = new ThemePreference(js);

        await preference.InitialiseAsync(systemPrefersDark: false);

        Assert.Equal(Accent.Purple, preference.Accent);
    }

    [Fact]
    public async Task SettingAnAccentPersistsItApartFromTheModeAndRaisesChanged()
    {
        var js = new FakeJsRuntime { Stored = nameof(ThemeMode.Dark) };
        var preference = new ThemePreference(js);
        await preference.InitialiseAsync(systemPrefersDark: false);
        var raised = 0;
        preference.Changed += () => raised++;

        await preference.SetAccentAsync(Accent.Blue);

        Assert.Equal(Accent.Blue, preference.Accent);
        Assert.Equal(nameof(Accent.Blue), js.Storage["pspad.accent"]);
        Assert.Equal(ThemeMode.Dark, preference.Mode);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task TheThemeFollowsTheAccent()
    {
        var preference = new ThemePreference(new FakeJsRuntime());
        await preference.InitialiseAsync(systemPrefersDark: false);

        await preference.SetAccentAsync(Accent.Orange);

        Assert.Same(PSPadTheme.For(Accent.Orange), preference.Theme);
    }

    [Fact]
    public async Task ACustomColourIsStoredRestoredAndDrivesTheTheme()
    {
        var js = new FakeJsRuntime();
        var preference = new ThemePreference(js);
        await preference.InitialiseAsync(systemPrefersDark: false);

        await preference.SetCustomAccentAsync("#1565c0");

        Assert.Equal(Accent.Custom, preference.Accent);
        Assert.Equal("#1565c0", preference.CustomColor);
        Assert.Equal(
            PSPadTheme.ForCustom("#1565c0").PaletteLight.Primary.ToString(),
            preference.Theme.PaletteLight.Primary.ToString());

        var restored = new ThemePreference(js);
        await restored.InitialiseAsync(systemPrefersDark: false);
        Assert.Equal(Accent.Custom, restored.Accent);
        Assert.Equal("#1565c0", restored.CustomColor);
    }

    [Fact]
    public async Task PickingAPresetAfterACustomColourKeepsTheColourForLater()
    {
        var js = new FakeJsRuntime();
        var preference = new ThemePreference(js);
        await preference.InitialiseAsync(systemPrefersDark: false);
        await preference.SetCustomAccentAsync("#1565c0");

        await preference.SetAccentAsync(Accent.Teal);

        Assert.Same(PSPadTheme.For(Accent.Teal), preference.Theme);
        Assert.Equal("#1565c0", preference.CustomColor);
    }

    [Fact]
    public async Task AStoredCustomAccentWithAGarbledColourFallsBackToGreen()
    {
        var js = new FakeJsRuntime();
        js.Storage["pspad.accent"] = nameof(Accent.Custom);
        js.Storage["pspad.accent.custom"] = "not a colour";
        var preference = new ThemePreference(js);

        await preference.InitialiseAsync(systemPrefersDark: false);

        Assert.Equal(Accent.Green, preference.Accent);
        Assert.Null(preference.CustomColor);
    }
}
