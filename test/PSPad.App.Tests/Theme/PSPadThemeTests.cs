using MudBlazor.Utilities;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class PSPadThemeTests
{
    [Fact]
    public void TheSameAccentHandsBackTheSameTheme() =>
        Assert.Same(PSPadTheme.For(Accent.Blue), PSPadTheme.For(Accent.Blue));

    [Fact]
    public void CustomIsNotAPreset() => Assert.DoesNotContain(Accent.Custom, PSPadTheme.Presets);

    [Fact]
    public void AReadableCustomColourIsUsedAsPicked() =>
        Assert.Equal("#1565c0", PSPadTheme.ForCustom("#1565c0").PaletteLight.Primary.ToString(MudColorOutputFormats.Hex).ToLowerInvariant());

    [Fact]
    public void ACustomColourTooPaleForLightModeIsDeepenedNotReplaced()
    {
        var primary = PSPadTheme.ForCustom("#ffeb3b").PaletteLight.Primary;
        var picked = new MudColor("#ffeb3b");

        Assert.NotEqual(picked.Value, primary.Value);
        Assert.InRange(Math.Abs(primary.H - picked.H), 0, 2);
    }
}
