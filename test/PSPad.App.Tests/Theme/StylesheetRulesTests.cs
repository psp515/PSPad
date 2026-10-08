using System.Text.RegularExpressions;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Theme;

[UnitTest]
public partial class StylesheetRulesTests
{
    static readonly string Css = File.ReadAllText(StylesheetPath());

    [Fact]
    public void TheCardTileShowsWhereKeyboardFocusIs()
    {
        var focus = Declarations(".pspad-card-header .pspad-card-tile.mud-icon-button:focus-visible");

        Assert.Contains("outline: 2px solid var(--mud-palette-primary)", focus);
        Assert.Contains("outline-offset: 2px", focus);
    }

    [Fact]
    public void TheCardTileDeepensItsTintOnHover() =>
        Assert.Contains(
            "background-color: color-mix(in srgb, var(--mud-palette-primary) 20%, transparent)",
            Declarations(".pspad-card-header .pspad-card-tile.mud-icon-button:hover"));

    [Fact]
    public void ThePhoneWeekStripOutranksTheCardLookAndStaysEdgeToEdge()
    {
        var strip = Declarations(".pspad-week-strip.mud-paper.mud-paper-outlined");

        Assert.Contains("border-radius: 0", strip);
        Assert.Contains("border-width: 0 0 1px 0", strip);
        Assert.Contains("box-shadow: none", strip);
    }

    [Theory]
    [InlineData(".mud-paper.pspad-goal-summary:hover")]
    [InlineData(".mud-paper.pspad-goal-summary:focus-visible")]
    public void TheGoalSummaryWashOutranksTheCardGround(string selector) =>
        Assert.Contains("background-color: var(--mud-palette-action-default-hover)", Declarations(selector));

    [Fact]
    public void WeekDaysAndShowAllKeepTheirOwnShapeInsteadOfThePill() =>
        Assert.Contains("border-radius: 999px", Declarations(".mud-button-root:not(.mud-icon-button, .pspad-week-day, .pspad-show-all)"));

    [Fact]
    public void NoButtonPillRuleCatchesTheWeekDays() =>
        Assert.Empty(Declarations(".mud-button-root:not(.mud-icon-button)"));

    [Theory]
    [InlineData(".mud-drawer .mud-paper.mud-paper-outlined")]
    [InlineData(".mud-drawer .mud-expansion-panels")]
    public void CardsInsideADrawerSitOnTheDrawerSurfaceNotBelowIt(string selector)
    {
        var inDrawer = Declarations(selector);

        Assert.Contains("background-color: var(--mud-palette-surface)", inDrawer);
        Assert.Contains("box-shadow: none", inDrawer);
    }

    [Theory]
    [InlineData("pspad-day-task")]
    [InlineData("pspad-task-card")]
    [InlineData("pspad-goal-card")]
    [InlineData("pspad-inbox-card")]
    public void AClickableCardShowsKeyboardFocusLikeHover(string card)
    {
        var hover = Declarations($".mud-paper-outlined.{card}:hover");

        Assert.NotEmpty(hover);
        Assert.Equal(hover, Declarations($".mud-paper-outlined.{card}:focus-visible"));
        Assert.Equal(hover, Declarations($".mud-paper-outlined.{card}:has(:focus-visible)"));
    }

    [Fact]
    public void ARowShowsKeyboardFocusLikeHover()
    {
        var hover = Declarations(".pspad-row:hover");

        Assert.NotEmpty(hover);
        Assert.Equal(hover, Declarations(".pspad-row:has(:focus-visible)"));
    }

    static string Declarations(string selector) =>
        string.Join("\n", Rule().Matches(Css)
            .Where(rule => SelectorsOf(rule).Contains(selector))
            .Select(rule => rule.Groups["body"].Value.Trim()));

    static IEnumerable<string> SelectorsOf(Match rule) =>
        TopLevelCommas().Split(rule.Groups["selectors"].Value).Select(selector => Whitespace().Replace(selector, " ").Trim());

    static string StylesheetPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "src", "PSPad.App", "wwwroot", "css", "app.css")))
            directory = directory.Parent!;

        return Path.Combine(directory.FullName, "src", "PSPad.App", "wwwroot", "css", "app.css");
    }

    [GeneratedRegex(@"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@",(?![^(]*\))")]
    private static partial Regex TopLevelCommas();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
