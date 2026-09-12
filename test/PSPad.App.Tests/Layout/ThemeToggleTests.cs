using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ThemeToggleTests : Bunit.TestContext
{
    [Fact]
    public void ItLabelsItselfWithTheCurrentMode()
    {
        Arrange();

        var toggle = Render<ThemeToggle>();

        Assert.Equal("Theme: System", toggle.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task TheLabelFollowsTheModeAsItCycles()
    {
        Arrange();

        var toggle = Render<ThemeToggle>();
        await toggle.Find("button").ClickAsync(new());

        Assert.Equal("Theme: Light", toggle.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task ClickingItCyclesTheMode()
    {
        var preference = Arrange();

        var toggle = Render<ThemeToggle>();
        await toggle.Find("button").ClickAsync(new());

        Assert.Equal(ThemeMode.Light, preference.Mode);
    }

    ThemePreference Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        var preference = new ThemePreference(JSInterop.JSRuntime);
        preference.InitialiseAsync(systemPrefersDark: false).GetAwaiter().GetResult();
        Services.AddSingleton(preference);

        return preference;
    }
}
