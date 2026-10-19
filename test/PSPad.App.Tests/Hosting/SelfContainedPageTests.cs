using System.Text.RegularExpressions;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Hosting;

[UnitTest]
public partial class SelfContainedPageTests
{
    static readonly string Index = File.ReadAllText(Path.Combine(RepositoryPaths.Wwwroot, "index.html"));

    [Fact]
    public void TheShellLoadsNothingFromAnotherOrigin()
    {
        Assert.Empty(ExternalReferences().Matches(Index));
    }

    [Fact]
    public void TheShellHasNoInlineEventHandlersTheContentSecurityPolicyWouldBlock()
    {
        Assert.Empty(InlineHandlers().Matches(Index));
    }

    [Fact]
    public void TheShellLoadsTheBundledFonts()
    {
        Assert.Contains("href=\"css/fonts.css\"", Index);
    }

    [Fact]
    public void EveryBundledFontFaceShipsInWwwroot()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryPaths.Wwwroot, "css", "fonts.css"));
        var files = FontUrls().Matches(css).Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(8, files.Count);
        Assert.All(files, file =>
            Assert.True(File.Exists(Path.GetFullPath(Path.Combine(RepositoryPaths.Wwwroot, "css", file))), file));
    }

    [Fact]
    public void TheBundledFontsShipWithTheirLicence()
    {
        Assert.True(File.Exists(Path.Combine(RepositoryPaths.Wwwroot, "fonts", "roboto", "OFL.txt")));
    }

    [Fact]
    public void BlazorStartsInTheCachedLanguageSoTheCultureNeverChangesAtBoot()
    {
        Assert.Contains("autostart=\"false\"", Index);
        Assert.Contains("Blazor.start(", Index);
        Assert.Contains("applicationCulture", Index);
        Assert.Contains("document.documentElement.lang = language", Index);
        Assert.Contains("Blazor.start(", Index);
        Assert.Contains(".catch(", Index);
    }

    [GeneratedRegex("""(?:src|href)\s*=\s*["'](?:https?:)?//""", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalReferences();

    [GeneratedRegex("""<[^>]+\son[a-z]+\s*=""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandlers();

    [GeneratedRegex("""url\(["']?([^)"']+)["']?\)""")]
    private static partial Regex FontUrls();
}
