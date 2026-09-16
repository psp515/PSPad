using MudBlazor;

namespace PSPad.App.Theme;

public static class PSPadTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4E7A5E",
            Secondary = "#6E8F7C",
            Error = "#B3261E",
            Warning = "#B26A00",
            Success = "#4E7A5E",
            Background = "#F7F8F5",
            Surface = "#FFFFFF",
            AppbarBackground = "#EDF1EA",
            AppbarText = "#1E2A22",
            DrawerBackground = "#EDF1EA",
            DrawerText = "#1E2A22",
            DrawerIcon = "#4E7A5E",
            LinesDefault = "#738D68",
            TableLines = "#738D68",
            TextPrimary = "#1E2A22",
            TextSecondary = "#66736B",
            ActionDefault = "#66736B"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#8FBF9F",
            Secondary = "#7FAE94",
            Error = "#F2A9A2",
            Warning = "#E0B252",
            Success = "#8FBF9F",
            Background = "#141815",
            Surface = "#1C211D",
            AppbarBackground = "#171C18",
            AppbarText = "#E4E9E4",
            DrawerBackground = "#171C18",
            DrawerText = "#E4E9E4",
            DrawerIcon = "#8FBF9F",
            LinesDefault = "#65766A",
            TableLines = "#65766A",
            TextPrimary = "#E4E9E4",
            TextSecondary = "#94A199",
            ActionDefault = "#94A199"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px"
        }
    };
}
