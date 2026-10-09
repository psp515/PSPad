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

    [Fact]
    public void ThePersistentSidebarIsPinnedToTheTopInsteadOfItsStaticPosition() =>
        Assert.Contains("top: 0", Declarations(".mud-drawer.mud-drawer-persistent.mud-drawer-pos-left"));

    [Fact]
    public void OnTheDesktopTheMainAreaContainsItsChildrensMargins()
    {
        var main = DeclarationsInMedia("min-width: 960px", ".mud-main-content");

        Assert.Contains("padding-top: 0 !important", main);
        Assert.Contains("display: flow-root", main);
    }

    [Fact]
    public void ThePageIconIsATintedTile()
    {
        var tile = Declarations(".pspad-page-icon");

        Assert.Contains("width: 40px", tile);
        Assert.Contains("height: 40px", tile);
        Assert.Contains("border-radius: 12px", tile);
        Assert.Contains("background-color: var(--pspad-tint)", tile);
    }

    [Fact]
    public void ThePhoneTopBarIconTileIsSmaller()
    {
        var tile = Declarations(".pspad-top-bar .pspad-page-icon");

        Assert.Contains("width: 36px", tile);
        Assert.Contains("height: 36px", tile);
        Assert.Contains("border-radius: 10px", tile);
    }

    [Fact]
    public void TheDesktopTitleIsTwentyPixelsSemiBoldWithTheSubtitleTwoPixelsUnderIt()
    {
        var title = Declarations(".pspad-page-title.mud-typography");
        var subtitle = Declarations(".pspad-page-subtitle.mud-typography");

        Assert.Contains("font-size: 20px", title);
        Assert.Contains("font-weight: 600", title);
        Assert.Contains("line-height: 1.15", title);
        Assert.Contains("font-size: 12.5px", subtitle);
        Assert.Contains("margin-top: 2px", subtitle);
        Assert.DoesNotContain("align-self", subtitle);
    }

    [Fact]
    public void TheDesktopHeadingRowIsAsTallAsTheAccountBadgeRowWithFourteenPixelGaps()
    {
        var row = Declarations(".pspad-page-heading");

        Assert.Contains("min-height: 48px", row);
        Assert.Contains("gap: 14px", row);
    }

    [Fact]
    public void ThePhoneTopBarIsSixtyFourPixelsOnTheSurfaceWithAHairline()
    {
        var bar = Declarations(".pspad-top-bar.mud-appbar");
        var toolbar = Declarations(".pspad-top-bar.mud-appbar .mud-toolbar-appbar");

        Assert.Contains("--pspad-top-bar-height: 64px", Declarations(":root"));
        Assert.Contains("background-color: var(--mud-palette-surface)", bar);
        Assert.Contains("border-bottom: 1px solid var(--pspad-line)", bar);
        Assert.Contains("height: var(--pspad-top-bar-height)", toolbar);
        Assert.Contains("padding: 0 16px", toolbar);
        Assert.Contains("gap: 12px", toolbar);
        Assert.Contains("padding-left: 4px", Declarations(".pspad-top-bar.mud-appbar .mud-toolbar-appbar:has(.pspad-top-back)"));
    }

    [Fact]
    public void TheTopBarHeightRuleOutranksMudBlazorsAppBarHeights() =>
        Assert.Empty(Declarations(".pspad-top-bar .mud-toolbar-appbar"));

    [Fact]
    public void PhoneContentStartsUnderTheTallerTopBar() =>
        Assert.Contains("padding-top: var(--pspad-top-bar-height)", Declarations(".pspad-top-bar ~ .mud-main-content"));

    [Fact]
    public void ThePullIndicatorSitsJustUnderTheTopBar() =>
        Assert.Contains("top: calc(var(--pspad-top-bar-height) + 10px)", Declarations(".pspad-pull-indicator"));

    [Fact]
    public void BelowSmallTheTimeLabelSitsAboveFullWidthPickers()
    {
        var media = Regex.Match(Css, @"@media \(max-width: 599\.98px\) \{(?<body>(?:[^{}]*\{[^{}]*\})*[^{}]*)\}");
        var inMedia = string.Join("\n", Rule().Matches(media.Groups["body"].Value)
            .Where(rule => SelectorsOf(rule).Contains(".pspad-task-time"))
            .Select(rule => rule.Groups["body"].Value));

        Assert.Contains("flex-direction: column", inMedia);
    }

    [Fact]
    public void ThePhoneTopBarTitleIsEighteenPixelsInTheTextColour()
    {
        var title = Declarations(".pspad-top-title .pspad-top-heading.mud-typography");

        Assert.Contains("font-size: 18px", title);
        Assert.Contains("font-weight: 600", title);
        Assert.Contains("color: var(--mud-palette-text-primary)", title);
        Assert.Contains("font-size: 12.5px", Declarations(".pspad-top-title .pspad-top-subtitle.mud-typography"));
    }

    [Fact]
    public void ThePhoneTopBarButtonsAreFortyFourPixels()
    {
        var buttons = Declarations(".pspad-top-bar .mud-icon-button");

        Assert.Contains("width: 44px", buttons);
        Assert.Contains("height: 44px", buttons);
    }

    [Fact]
    public void TheFromAndToLabelsAreSmallAndFixedWidth()
    {
        var label = Declarations(".pspad-time-line-label");

        Assert.Contains("font-size: 12.5px", label);
        Assert.Contains("flex: 0 0 40px", label);
        Assert.Contains("color: var(--mud-palette-text-secondary)", label);
    }

    [Fact]
    public void EachTimePickerFillsTheRestOfItsRow() =>
        Assert.Contains("flex: 1 1 auto", Declarations(".pspad-time-line .pspad-time-input"));

    [Fact]
    public void StatusBeltsStackInFlowSoTheyPushThePageDown()
    {
        var stack = Declarations(".pspad-belts");

        Assert.Contains("display: flex", stack);
        Assert.Contains("flex-direction: column", stack);
        Assert.DoesNotContain("position: fixed", stack);
        Assert.DoesNotContain("position: absolute", stack);
    }

    [Fact]
    public void OnTheDesktopBeltsAreRoundedAndSpacedAboveTheHeading()
    {
        var stack = DeclarationsInMedia("min-width: 960px", ".pspad-belts");
        var belt = DeclarationsInMedia("min-width: 960px", ".pspad-belt.mud-alert");
        var below = DeclarationsInMedia("min-width: 960px", ".pspad-belts + .pspad-content");

        Assert.Contains("gap: 8px", stack);
        Assert.Contains("padding: 24px 24px 0", stack);
        Assert.Contains("border-radius: 10px", belt);
        Assert.Contains("padding-top: 12px !important", below);
    }

    [Fact]
    public void OnAPhoneBeltsRunEdgeToEdgeUnderTheTopBar()
    {
        var belt = DeclarationsInMedia("max-width: 959.98px", ".pspad-belt.mud-alert");

        Assert.Contains("border-radius: 0", belt);
        Assert.Contains("border-bottom: 1px solid var(--pspad-line)", belt);
    }

    [Fact]
    public void ABeltWritesInBodyTextOnItsTint()
    {
        var belt = Declarations(".pspad-belt.mud-alert");

        Assert.Contains("color: var(--mud-palette-text-primary)", belt);
        Assert.Contains("background-color: color-mix(in srgb, var(--pspad-belt-tone) 14%, var(--mud-palette-background))", belt);
    }

    static string Declarations(string selector) =>
        string.Join("\n", Rule().Matches(Css)
            .Where(rule => SelectorsOf(rule).Contains(selector))
            .Select(rule => rule.Groups["body"].Value.Trim()));

    static string DeclarationsInMedia(string condition, string selector) =>
        string.Join("\n", Regex.Matches(Css, @"@media \(" + Regex.Escape(condition) + @"\) \{(?<body>(?:[^{}]*\{[^{}]*\})*[^{}]*)\}")
            .SelectMany(media => Rule().Matches(media.Groups["body"].Value))
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
