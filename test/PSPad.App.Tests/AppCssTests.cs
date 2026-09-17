using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class AppCssTests
{
    static readonly string Css = File.ReadAllText(PathToAppCss());

    [Fact]
    public void ThePspadGridColumnCountStepsWithMudBlazorsSmAndMdBreakpoints()
    {
        // A fixed pixel track width (auto-fit, 340px) wraps to fewer columns
        // than the container can actually fit whenever the exact multiple
        // doesn't divide evenly -- three 340px cards need 1052px, so a
        // 900px-wide area wraps to two even though there's room for a third,
        // narrower one. Tying the column count to breakpoints instead (one
        // column below sm, two below md, three at md and up -- matching
        // BrowserViewport's own Breakpoint.MdAndUp split) guarantees the
        // column count the space allows, with each card taking an even share
        // rather than stretching into a row that has fewer cards than slots.
        Assert.Contains("grid-template-columns: repeat(1, 1fr);", Css);
        Assert.Contains("@media (min-width: 600px)", Css);
        Assert.Contains("grid-template-columns: repeat(2, 1fr);", Css);
        Assert.Contains("@media (min-width: 960px)", Css);
        Assert.Contains("grid-template-columns: repeat(3, 1fr);", Css);
        Assert.Contains("@media (min-width: 1920px)", Css);
        Assert.Contains("grid-template-columns: repeat(4, 1fr);", Css);
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
