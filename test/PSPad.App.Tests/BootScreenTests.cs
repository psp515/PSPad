using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class BootScreenTests
{
    static readonly string Markup = File.ReadAllText(PathToIndex());

    [Fact]
    public void ItCarriesTheBrandCheckPath()
    {
        Assert.Contains("M184,272 l48,48 l96,-120", Markup);
    }

    [Fact]
    public void ItKeepsNoTemplateLoaderLeftovers()
    {
        Assert.DoesNotContain("loading-progress", Markup);
    }

    [Fact]
    public void ItStampsTheStoredThemeBeforeFirstPaint()
    {
        Assert.Contains("pspad.theme", Markup);
    }

    static string PathToIndex()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory!.FullName, "src", "PSPad.App", "wwwroot", "index.html");
    }
}
