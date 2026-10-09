using MudBlazor;
using MudBlazor.Utilities;

namespace PSPad.App.Theme;

public static class PSPadTheme
{
    public const string LightRaised = "#FFFFFF";
    public const string DarkRaised = "#1B1B1B";

    sealed record Shades(string Light, string LightSecondary, string Dark, string DarkSecondary);

    static readonly Dictionary<Accent, Shades> AccentShades = new()
    {
        [Accent.Green] = new(Colors.Green.Darken3, Colors.Green.Darken4, Colors.Green.Lighten2, Colors.Green.Lighten4),
        [Accent.Teal] = new(Colors.Teal.Darken3, Colors.Teal.Darken4, Colors.Teal.Lighten2, Colors.Teal.Lighten4),
        [Accent.Blue] = new(Colors.Blue.Darken3, Colors.Blue.Darken4, Colors.Blue.Lighten2, Colors.Blue.Lighten4),
        [Accent.Indigo] = new(Colors.Indigo.Default, Colors.Indigo.Darken3, Colors.Indigo.Lighten2, Colors.Indigo.Lighten4),
        [Accent.Purple] = new(Colors.DeepPurple.Default, Colors.DeepPurple.Darken3, Colors.DeepPurple.Lighten3, Colors.DeepPurple.Lighten4),
        [Accent.Pink] = new(Colors.Pink.Darken2, Colors.Pink.Darken4, Colors.Pink.Lighten2, Colors.Pink.Lighten4),
        [Accent.Orange] = new(Colors.DeepOrange.Darken4, Colors.Brown.Darken2, Colors.Orange.Lighten2, Colors.Orange.Lighten4)
    };

    static readonly string LightBackground = Colors.Gray.Lighten4;
    static readonly string DarkText = Colors.Gray.Darken4;

    static readonly Dictionary<Accent, MudTheme> Themes =
        AccentShades.ToDictionary(pair => pair.Key, pair => Build(pair.Value));

    public static IReadOnlyList<Accent> Presets { get; } = [.. AccentShades.Keys];

    public static MudTheme For(Accent accent) => Themes[accent];

    public static MudTheme ForCustom(string color) => Build(Derive(new MudColor(color)));

    public static string Swatch(Accent accent) => AccentShades[accent].Light;

    static Shades Derive(MudColor picked)
    {
        var light = ColorContrast.DeepenUntil(picked, shade => ColorContrast.Ratio(shade, LightBackground) >= 4.5);
        var dark = ColorContrast.LightenUntil(picked, shade => ColorContrast.Ratio(shade, DarkText) >= 4.5);

        return new(
            Hex(light),
            Hex(light.SetL(Math.Max(0, light.L - 0.1))),
            Hex(dark),
            Hex(dark.SetL(Math.Min(1, dark.L + 0.15))));
    }

    static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.Hex);

    static MudTheme Build(Shades accent) => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = accent.Light,
            PrimaryContrastText = Colors.Shades.White,
            Secondary = accent.LightSecondary,
            SecondaryContrastText = Colors.Shades.White,
            Error = Colors.Red.Darken2,
            Warning = Colors.Orange.Darken4,
            Success = Colors.Green.Darken3,
            Info = Colors.Blue.Darken2,
            Background = LightBackground,
            Surface = Colors.Shades.White,
            AppbarBackground = Colors.Shades.White,
            AppbarText = Colors.Gray.Darken4,
            DrawerBackground = Colors.Shades.White,
            DrawerText = Colors.Gray.Darken4,
            DrawerIcon = accent.Light,
            LinesDefault = Colors.Gray.Darken1,
            TableLines = Colors.Gray.Darken1,
            TextPrimary = Colors.Gray.Darken4,
            TextSecondary = Colors.Gray.Darken2,
            ActionDefault = Colors.Gray.Darken2
        },
        PaletteDark = new PaletteDark
        {
            Primary = accent.Dark,
            PrimaryContrastText = Colors.Gray.Darken4,
            Secondary = accent.DarkSecondary,
            SecondaryContrastText = Colors.Gray.Darken4,
            Error = Colors.Red.Lighten2,
            Warning = Colors.Orange.Lighten2,
            Success = Colors.Green.Lighten2,
            Info = Colors.Blue.Lighten2,
            Background = "#121212",
            Surface = "#1E1E1E",
            AppbarBackground = "#272727",
            AppbarText = Colors.Gray.Lighten3,
            DrawerBackground = "#1E1E1E",
            DrawerText = Colors.Gray.Lighten3,
            DrawerIcon = accent.Dark,
            LinesDefault = Colors.Gray.Darken1,
            TableLines = Colors.Gray.Darken1,
            TextPrimary = Colors.Gray.Lighten3,
            TextSecondary = Colors.Gray.Lighten1,
            ActionDefault = Colors.Gray.Lighten1
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "260px"
        }
    };
}
