using MudBlazor;

namespace PSPad.App.Theme;

public static class PSPadTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5",
            Secondary = "#0EA5E9",
            Error = "#DC2626",
            Warning = "#D97706",
            Success = "#16A34A",
            Background = "#FAFAFA",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1F2328",
            DrawerBackground = "#FAFAFA",
            DrawerText = "#1F2328",
            TextPrimary = "#1F2328",
            TextSecondary = "#5B6472"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#818CF8",
            Secondary = "#38BDF8",
            Error = "#F87171",
            Warning = "#FBBF24",
            Success = "#4ADE80",
            Background = "#121417",
            Surface = "#1A1D21",
            AppbarBackground = "#1A1D21",
            AppbarText = "#E6E8EB",
            DrawerBackground = "#121417",
            DrawerText = "#E6E8EB",
            TextPrimary = "#E6E8EB",
            TextSecondary = "#9BA4B0"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        }
    };
}
