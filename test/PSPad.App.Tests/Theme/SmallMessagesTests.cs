using MudBlazor;
using PSPad.App.Theme;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public class SmallMessagesTests
{
    static SnackbarConfiguration Configured()
    {
        var configuration = new SnackbarConfiguration();
        SmallMessages.Configure(configuration);
        return configuration;
    }

    [Fact]
    public void OnlyOneMessageShowsAtATime()
    {
        var configuration = Configured();

        Assert.Equal(1, configuration.MaxDisplayedSnackbars);
        Assert.True(configuration.NewestOnTop);
        Assert.True(configuration.PreventDuplicates);
    }

    [Fact]
    public void AMessageStaysFourSecondsAndCanBeClosed()
    {
        var configuration = Configured();

        Assert.Equal(4000, configuration.VisibleStateDuration);
        Assert.True(configuration.ShowCloseIcon);
    }

    [Fact]
    public void AMessageComesAndGoesQuickly()
    {
        var configuration = Configured();

        Assert.InRange(configuration.ShowTransitionDuration, 1, 250);
        Assert.InRange(configuration.HideTransitionDuration, 1, 250);
    }

    [Fact]
    public void MessagesSitAtTheBottomCentreOnASolidGround()
    {
        var configuration = Configured();

        Assert.Equal(Defaults.Classes.Position.BottomCenter, configuration.PositionClass);
        Assert.Equal(Variant.Text, configuration.SnackbarVariant);
        Assert.Equal(100, configuration.MaximumOpacity);
    }

    [Fact]
    public void BothLayoutsShareTheConfigurationThroughProgram()
    {
        var program = File.ReadAllText(Path.Combine(AppRoot(), "Program.cs"));

        Assert.Contains("AddMudServices(config => SmallMessages.Configure(config.SnackbarConfiguration))", program);
    }

    static string AppRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "src", "PSPad.App", "Program.cs")))
            directory = directory.Parent!;

        return Path.Combine(directory.FullName, "src", "PSPad.App");
    }
}
