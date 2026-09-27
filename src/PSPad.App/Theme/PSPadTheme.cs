using MudBlazor;

namespace PSPad.App.Theme;

public static class PSPadTheme
{
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

    static readonly Dictionary<Accent, MudTheme> Themes =
        Enum.GetValues<Accent>().ToDictionary(accent => accent, accent => Build(AccentShades[accent]));

    public static MudTheme For(Accent accent) => Themes[accent];

    public static string Swatch(Accent accent) => AccentShades[accent].Light;

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
            Background = Colors.Gray.Lighten4,
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
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        }
    };
}
