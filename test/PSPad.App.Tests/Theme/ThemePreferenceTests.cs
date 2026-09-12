using Microsoft.JSInterop;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class ThemePreferenceTests
{
    sealed class FakeJsRuntime : IJSRuntime
    {
        public List<string> Calls { get; } = [];

        public string? Stored { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);

            if (identifier == "localStorage.setItem")
            {
                Stored = args?[1] as string;
            }

            return ValueTask.FromResult((TValue)(object)(Stored ?? "")!);
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
}
