using MudBlazor;
using MudBlazor.Utilities;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class PSPadThemeTests
{
    // WCAG 1.4.11 asks 3:1 of borders, dividers and icons; WCAG 1.4.3 asks 4.5:1 of text.
    const double MinimumUiContrast = 3.0;
    const double MinimumTextContrast = 4.5;

    public static TheoryData<Accent, bool> EveryPalette()
    {
        var data = new TheoryData<Accent, bool>();
        foreach (var accent in Enum.GetValues<Accent>())
        {
            data.Add(accent, false);
            data.Add(accent, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void LinesAreVisibleAgainstEveryGround(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.LinesDefault, palette.Surface) >= MinimumUiContrast);
        Assert.True(Contrast(palette.LinesDefault, palette.Background) >= MinimumUiContrast);
        Assert.True(Contrast(palette.LinesDefault, palette.DrawerBackground) >= MinimumUiContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TheAccentReadsAsTextOnEveryGround(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.Primary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Primary, palette.Background) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Primary, palette.DrawerBackground) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Secondary, palette.Surface) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TextOnAFilledAccentIsReadable(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.PrimaryContrastText, palette.Primary) >= MinimumTextContrast);
        Assert.True(Contrast(palette.SecondaryContrastText, palette.Secondary) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TheNavigationReadsClearly(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.DrawerText, palette.DrawerBackground) >= MinimumTextContrast);
        Assert.True(Contrast(palette.DrawerIcon, palette.DrawerBackground) >= MinimumUiContrast);
        Assert.True(Contrast(palette.AppbarText, palette.AppbarBackground) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void BodyTextReadsOnEveryGround(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.TextPrimary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.TextSecondary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.TextSecondary, palette.Background) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Error, palette.Surface) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void StatusColoursDoNotFollowTheAccent(Accent accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var reference = PaletteOf(Accent.Green, dark);

        Assert.Equal(reference.Success.ToString(), palette.Success.ToString());
        Assert.Equal(reference.Error.ToString(), palette.Error.ToString());
        Assert.Equal(reference.Warning.ToString(), palette.Warning.ToString());
    }

    [Fact]
    public void EachAccentGetsItsOwnPrimary()
    {
        var primaries = Enum.GetValues<Accent>()
            .Select(accent => PSPadTheme.For(accent).PaletteLight.Primary.ToString())
            .ToList();

        Assert.Equal(primaries.Count, primaries.Distinct().Count());
    }

    [Fact]
    public void TheSameAccentHandsBackTheSameTheme() =>
        Assert.Same(PSPadTheme.For(Accent.Blue), PSPadTheme.For(Accent.Blue));

    static Palette PaletteOf(Accent accent, bool dark) =>
        dark ? PSPadTheme.For(accent).PaletteDark : PSPadTheme.For(accent).PaletteLight;

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
