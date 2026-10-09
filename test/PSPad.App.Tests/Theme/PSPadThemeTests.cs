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

    static readonly string[] AwkwardCustomColours =
        ["#ffeb3b", "#0000ff", "#101010", "#f5f5f5", "#ff00ff", "#00ffff", "#808080", "#ffffff", "#000000"];

    public static TheoryData<string, bool> EveryPalette()
    {
        var data = new TheoryData<string, bool>();
        foreach (var accent in PSPadTheme.Presets.Select(preset => preset.ToString()).Concat(AwkwardCustomColours))
        {
            data.Add(accent, false);
            data.Add(accent, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void LinesAreVisibleAgainstEveryGround(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.LinesDefault, palette.Surface) >= MinimumUiContrast);
        Assert.True(Contrast(palette.LinesDefault, palette.Background) >= MinimumUiContrast);
        Assert.True(Contrast(palette.LinesDefault, palette.DrawerBackground) >= MinimumUiContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TheAccentReadsAsTextOnEveryGround(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.Primary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Primary, palette.Background) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Primary, palette.DrawerBackground) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Secondary, palette.Surface) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TextOnAFilledAccentIsReadable(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.PrimaryContrastText, palette.Primary) >= MinimumTextContrast);
        Assert.True(Contrast(palette.SecondaryContrastText, palette.Secondary) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TheNavigationReadsClearly(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.DrawerText, palette.DrawerBackground) >= MinimumTextContrast);
        Assert.True(Contrast(palette.DrawerIcon, palette.DrawerBackground) >= MinimumUiContrast);
        Assert.True(Contrast(palette.AppbarText, palette.AppbarBackground) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void BodyTextReadsOnEveryGround(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);

        Assert.True(Contrast(palette.TextPrimary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.TextSecondary, palette.Surface) >= MinimumTextContrast);
        Assert.True(Contrast(palette.TextSecondary, palette.Background) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Error, palette.Surface) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void StatusColoursDoNotFollowTheAccent(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var reference = PaletteOf(nameof(Accent.Green), dark);

        Assert.Equal(reference.Success.ToString(), palette.Success.ToString());
        Assert.Equal(reference.Error.ToString(), palette.Error.ToString());
        Assert.Equal(reference.Warning.ToString(), palette.Warning.ToString());
        Assert.Equal(reference.Info.ToString(), palette.Info.ToString());
        Assert.Equal(reference.TextPrimary.ToString(), palette.TextPrimary.ToString());
        Assert.Equal(reference.TextSecondary.ToString(), palette.TextSecondary.ToString());
        Assert.Equal(reference.Background.ToString(), palette.Background.ToString());
    }

    [Fact]
    public void EachAccentGetsItsOwnPrimary()
    {
        var primaries = PSPadTheme.Presets
            .Select(accent => PSPadTheme.For(accent).PaletteLight.Primary.ToString())
            .ToList();

        Assert.Equal(primaries.Count, primaries.Distinct().Count());
    }

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

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void TheActiveSidebarLinkReadsOnItsTint(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var tint = Mix(palette.Primary, palette.DrawerBackground, 0.12);

        Assert.True(Contrast(palette.TextPrimary, tint) >= MinimumTextContrast);
        Assert.True(Contrast(palette.Primary, tint) >= MinimumUiContrast);
    }

    [Fact]
    public void CornersAreTwelvePixels() =>
        Assert.Equal("12px", PSPadTheme.For(Accent.Green).LayoutProperties.DefaultBorderRadius);

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void SelectedFillsReadOnTheRefreshTint(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var tint = Mix(palette.Primary, RaisedOf(palette, dark), 0.12);

        Assert.True(Contrast(palette.TextPrimary, tint) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void CardControlsAndMetaReadOnTheRaisedSurface(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var raised = RaisedOf(palette, dark);

        Assert.True(Contrast(palette.ActionDefault, raised) >= MinimumUiContrast);
        Assert.True(Contrast(palette.TextSecondary, raised) >= MinimumTextContrast);
    }

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void QuietChipsAndCountPillsReadOnTheHoverWash(string accent, bool dark)
    {
        var palette = PaletteOf(accent, dark);
        var wash = Mix(dark ? new MudColor("#FFFFFF") : new MudColor("#000000"), RaisedOf(palette, dark), 0.04);

        Assert.True(Contrast(palette.TextSecondary, wash) >= MinimumTextContrast);
    }

    const double BeltTintShare = 0.14;
    const double BeltInkShare = 0.6;

    public static TheoryData<bool, Severity> EveryGroundAndBelt()
    {
        var data = new TheoryData<bool, Severity>();
        foreach (var dark in new[] { false, true })
        {
            foreach (var severity in new[] { Severity.Info, Severity.Warning, Severity.Error })
            {
                data.Add(dark, severity);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryGroundAndBelt))]
    public void AStatusBeltReadsOnItsTint(bool dark, Severity severity)
    {
        var palette = PaletteOf(nameof(Accent.Green), dark);
        var tone = ToneOf(palette, severity);
        var tint = Mix(tone, palette.Background, BeltTintShare);
        var ink = Mix(tone, palette.TextPrimary, BeltInkShare);

        Assert.True(Contrast(palette.TextPrimary, tint) >= MinimumTextContrast);
        Assert.True(Contrast(ink, tint) >= MinimumTextContrast);
        Assert.True(Contrast(palette.TextSecondary, tint) >= MinimumUiContrast);
    }

    [Fact]
    public void TheBeltTintAndInkMatchTheStylesheet()
    {
        var css = File.ReadAllText(StylesheetPath());

        Assert.Contains("color-mix(in srgb, var(--pspad-belt-tone) 14%, var(--mud-palette-background))", css);
        Assert.Contains("color-mix(in srgb, var(--pspad-belt-tone) 60%, var(--mud-palette-text-primary))", css);
        Assert.Contains("--pspad-belt-tone: var(--mud-palette-info)", css);
        Assert.Contains("--pspad-belt-tone: var(--mud-palette-warning)", css);
        Assert.Contains("--pspad-belt-tone: var(--mud-palette-error)", css);
    }

    static MudColor ToneOf(Palette palette, Severity severity) => severity switch
    {
        Severity.Info => palette.Info,
        Severity.Warning => palette.Warning,
        _ => palette.Error
    };

    const string LightMessageGround = "#FFFFFF";
    const string DarkMessageGround = "#2C2C2C";

    [Theory]
    [MemberData(nameof(EveryGroundAndBelt))]
    public void ASmallMessageReadsOnItsGround(bool dark, Severity severity)
    {
        var palette = PaletteOf(nameof(Accent.Green), dark);
        var ground = new MudColor(dark ? DarkMessageGround : LightMessageGround);

        Assert.True(Contrast(palette.TextPrimary, ground) >= MinimumTextContrast);
        Assert.True(Contrast(ToneOf(palette, severity), ground) >= MinimumUiContrast);
    }

    [Fact]
    public void TheMessageGroundMatchesTheStylesheet()
    {
        var css = File.ReadAllText(StylesheetPath());

        Assert.Contains($"--pspad-message-ground: {LightMessageGround}", css);
        Assert.Contains($"--pspad-message-ground: {DarkMessageGround}", css);
    }

    [Fact]
    public void TheRaisedGroundMatchesTheStylesheet()
    {
        var css = File.ReadAllText(StylesheetPath());

        Assert.Contains($"--pspad-raised: {PSPadTheme.LightRaised}", css);
        Assert.Contains($"--pspad-raised: {PSPadTheme.DarkRaised}", css);
    }

    static string StylesheetPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "src", "PSPad.App", "wwwroot", "css", "app.css")))
            directory = directory.Parent!;

        return Path.Combine(directory.FullName, "src", "PSPad.App", "wwwroot", "css", "app.css");
    }

    static MudColor RaisedOf(Palette palette, bool dark) => new(dark ? PSPadTheme.DarkRaised : PSPadTheme.LightRaised);

    static MudColor Mix(MudColor over, MudColor ground, double share) => new(
        (byte)Math.Round(share * over.R + (1 - share) * ground.R),
        (byte)Math.Round(share * over.G + (1 - share) * ground.G),
        (byte)Math.Round(share * over.B + (1 - share) * ground.B),
        (byte)255);

    static Palette PaletteOf(string accent, bool dark)
    {
        var theme = Enum.TryParse<Accent>(accent, out var preset) ? PSPadTheme.For(preset) : PSPadTheme.ForCustom(accent);
        return dark ? theme.PaletteDark : theme.PaletteLight;
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
