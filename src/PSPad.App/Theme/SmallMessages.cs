using MudBlazor;

namespace PSPad.App.Theme;

public static class SmallMessages
{
    public static void Configure(SnackbarConfiguration configuration)
    {
        configuration.PositionClass = Defaults.Classes.Position.BottomCenter;
        configuration.MaxDisplayedSnackbars = 1;
        configuration.NewestOnTop = true;
        configuration.PreventDuplicates = true;
        configuration.VisibleStateDuration = 4000;
        configuration.ShowTransitionDuration = 150;
        configuration.HideTransitionDuration = 150;
        configuration.ShowCloseIcon = true;
        configuration.SnackbarVariant = Variant.Text;
        configuration.MaximumOpacity = 100;
    }
}
