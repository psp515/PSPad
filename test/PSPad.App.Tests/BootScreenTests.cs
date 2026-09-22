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

    [Fact]
    public void ItScopesBootStylesToBootWrapperNotAppElement()
    {
        Assert.Contains(".pspad-boot {", Markup);
        Assert.DoesNotContain("#app {", Markup);
    }

    [Fact]
    public void ItKeepsTheSplashOutsideTheAppElementSoBlazorCannotDropIt()
    {
        var app = Markup.IndexOf("<div id=\"app\">", StringComparison.Ordinal);
        var boot = Markup.IndexOf("class=\"pspad-boot\"", StringComparison.Ordinal);

        Assert.True(boot > 0);
        Assert.True(boot < app || Markup.IndexOf("</div>", app, StringComparison.Ordinal) < boot);
    }

    [Fact]
    public void ItLoadsTheBootModuleThatTearsTheSplashDown()
    {
        Assert.Contains("js/boot.js", Markup);
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
