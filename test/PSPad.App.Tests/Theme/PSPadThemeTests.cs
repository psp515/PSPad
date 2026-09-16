using MudBlazor;
using MudBlazor.Utilities;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class PSPadThemeTests
{
    // WCAG 1.4.11 (non-text contrast): a border/divider that conveys structure
    // needs at least 3:1 against every surface it can sit on, not the 4.5:1
    // body-text threshold. LinesDefault previously measured ~1.2:1 against both
    // Surface and Background in each palette -- functionally invisible.
    const double MinimumUiContrast = 3.0;

    [Fact]
    public void LinesDefaultIsVisibleAgainstSurfaceInLightMode()
    {
        var palette = PSPadTheme.Instance.PaletteLight;

        Assert.True(Contrast(palette.LinesDefault, palette.Surface) >= MinimumUiContrast);
    }

    [Fact]
    public void LinesDefaultIsVisibleAgainstBackgroundInLightMode()
    {
        var palette = PSPadTheme.Instance.PaletteLight;

        Assert.True(Contrast(palette.LinesDefault, palette.Background) >= MinimumUiContrast);
    }

    [Fact]
    public void LinesDefaultIsVisibleAgainstSurfaceInDarkMode()
    {
        var palette = PSPadTheme.Instance.PaletteDark;

        Assert.True(Contrast(palette.LinesDefault, palette.Surface) >= MinimumUiContrast);
    }

    [Fact]
    public void LinesDefaultIsVisibleAgainstBackgroundInDarkMode()
    {
        var palette = PSPadTheme.Instance.PaletteDark;

        Assert.True(Contrast(palette.LinesDefault, palette.Background) >= MinimumUiContrast);
    }

    static double Contrast(MudColor a, MudColor b)
    {
        var lumA = RelativeLuminance(a);
        var lumB = RelativeLuminance(b);
        var (lighter, darker) = lumA >= lumB ? (lumA, lumB) : (lumB, lumA);

        return (lighter + 0.05) / (darker + 0.05);
    }

    static double RelativeLuminance(MudColor color) =>
        0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);

    static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
