using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class AppCssTests
{
    static readonly string Css = File.ReadAllText(PathToAppCss());

    [Fact]
    public void ThePspadGridDoesNotLeaveDanglingEmptyColumnsWhenItemsDontFillARow()
    {
        // auto-fill reserves a track for every column that fits the container
        // width even with nothing to put in it, so 2 cards in a 3-column-wide
        // area leave a third, cardless track painted in the grid's own
        // background -- auto-fit collapses that track and lets the real cards
        // fill the row instead.
        Assert.Contains("auto-fit", Css);
        Assert.DoesNotContain("auto-fill", Css);
    }

    static string PathToAppCss()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory!.FullName, "src", "PSPad.App", "wwwroot", "css", "app.css");
    }
}
