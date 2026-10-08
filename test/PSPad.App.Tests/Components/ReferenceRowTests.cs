using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.Tests;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ReferenceRowTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid ListId = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItShowsTheItemsName()
    {
        Arrange();
        var item = NewItem("Bigos");

        var row = Render(item);

        Assert.Contains("Bigos", row.Find(".pspad-row-name").TextContent);
    }

    [Fact]
    public void EveryFieldIsAChipInOrder()
    {
        Arrange();
        var item = NewItem("Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AddField(item, "Difficulty", "Easy");

        var row = Render(item);

        Assert.Equal(["Time: 45 min", "Servings: 4", "Difficulty: Easy"],
            row.FindAll(".pspad-row-chips .pspad-row-chip").Select(chip => chip.TextContent.Trim()));
    }

    [Fact]
    public void AnItemWithoutFieldsHasNoChipLine()
    {
        Arrange();

        var row = Render(NewItem("Bigos"));

        Assert.Empty(row.FindAll(".pspad-row-meta"));
    }

    [Fact]
    public void QuantitiesAreNormalisedInTheirChip()
    {
        Arrange();
        var item = NewItem("Filament");
        AddField(item, "Weight", "1000g");

        var row = Render(item);

        Assert.Equal("Weight: 1000 g", row.Find(".pspad-row-chip").TextContent.Trim());
    }

    [Fact]
    public void ALinkChipShowsOnlyItsLabelAndOpensTheUrlInANewTab()
    {
        Arrange();
        var item = NewItem("Manual");
        AddField(item, "Docs", "https://example.com/a/very/long/manual?utm_source=x");

        var row = Render(item);

        var link = row.Find("a.pspad-row-chip");
        Assert.Equal("Docs", link.TextContent.Trim());
        Assert.Equal("https://example.com/a/very/long/manual?utm_source=x", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.DoesNotContain("example.com", row.Find(".pspad-row-meta").TextContent);
    }

    [Fact]
    public void ALinkChipIsARealLinkThatSharesNoOpener()
    {
        Arrange();
        var item = NewItem("Manual");
        AddField(item, "Docs", "https://example.com/manual");

        var row = Render(item);

        Assert.Equal("a", row.Find(".pspad-row-chip-link").TagName.ToLowerInvariant());
        Assert.Contains("noopener", row.Find(".pspad-row-chip-link").GetAttribute("rel"));
    }

    [Fact]
    public void APathChipShowsItsLabelAndCopiesThePath()
    {
        Arrange();
        JSInterop.SetupModule("./js/clipboard.js").SetupVoid("copy", "/etc/hosts").SetVoidResult();
        var item = NewItem("Config");
        AddField(item, "Hosts", "/etc/hosts");
        var opened = false;

        var row = Render<ReferenceRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnOpen, (ReferenceItem _) => opened = true));
        var chip = row.Find(".pspad-row-chip-path");
        Assert.Equal("Hosts", chip.TextContent.Trim());
        chip.Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Copied") == true);
        Assert.False(opened);
    }

    [Fact]
    public void AnItemMarkedOnASnapshotShowsTheMarkChip()
    {
        Arrange();
        var item = NewItem("Bigos");
        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), true),
            DateTimeOffset.UnixEpoch));

        var row = Render(item);

        Assert.Contains("pspad-snapshot-mark", row.Markup);
    }

    [Fact]
    public void AnItemWithoutAMarkShowsNoChip()
    {
        Arrange();

        var row = Render(NewItem("Bigos"));

        Assert.DoesNotContain("pspad-snapshot-mark", row.Markup);
    }

    [Fact]
    public void ItSharesTheRowShapeWithAOneLineName()
    {
        Arrange();

        var row = Render(NewItem("Słuchawki Aktywne"));

        row.Find(".pspad-reference-row.pspad-row");
        Assert.Equal("Słuchawki Aktywne", row.Find(".pspad-row-name").GetAttribute("title"));
    }

    [Fact]
    public void ThereIsNoCheckbox()
    {
        Arrange();
        var item = NewItem("Bigos");

        var row = Render(item);

        Assert.Empty(row.FindAll(".mud-checkbox"));
    }

    [Fact]
    public void ClickingTheStarButtonRaisesOnStar()
    {
        Arrange();
        var item = NewItem("Bigos");
        ReferenceItem? starred = null;

        var row = Render<ReferenceRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnStar, (ReferenceItem starredItem) => starred = starredItem));
        row.Find(".pspad-row-star").Click();

        Assert.Same(item, starred);
    }

    [Fact]
    public void TheStarButtonIsLabelledStarWhenNotStarred()
    {
        Arrange();
        var item = NewItem("Bigos");

        var row = Render(item);

        Assert.Equal("Star", row.Find(".pspad-row-star").GetAttribute("aria-label"));
    }

    [Fact]
    public void TheStarButtonIsLabelledUnstarWhenStarred()
    {
        Arrange();
        var item = NewItem("Bigos");
        item.ApplyAll(ReferenceItem.Decide(
            item, new StarReferenceItem(Guid.NewGuid(), User, item.Id, true), DateTimeOffset.UnixEpoch));

        var row = Render(item);

        Assert.Equal("Unstar", row.Find(".pspad-row-star").GetAttribute("aria-label"));
    }

    [Fact]
    public void AnUnstarredStarIsQuiet()
    {
        Arrange();

        var star = Render(NewItem("Bigos")).FindComponent<MudIconButton>().Instance;

        Assert.Equal(Color.Default, star.Color);
        Assert.Contains("pspad-muted", star.Class);
    }

    [Fact]
    public void AStarredStarIsPrimary()
    {
        Arrange();
        var item = NewItem("Bigos");
        item.ApplyAll(ReferenceItem.Decide(
            item, new StarReferenceItem(Guid.NewGuid(), User, item.Id, true), DateTimeOffset.UnixEpoch));

        var star = Render(item).FindComponent<MudIconButton>().Instance;

        Assert.Equal(Color.Primary, star.Color);
        Assert.DoesNotContain("pspad-muted", star.Class);
    }

    [Fact]
    public void ClickingTheRowRaisesOnOpen()
    {
        Arrange();
        var item = NewItem("Bigos");
        ReferenceItem? opened = null;

        var row = Render<ReferenceRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnOpen, (ReferenceItem openedItem) => opened = openedItem));
        row.Find(".pspad-reference-name").Click();

        Assert.Same(item, opened);
    }

    IRenderedComponent<ReferenceRow> Render(ReferenceItem item) =>
        Render<ReferenceRow>(parameters => parameters.Add(p => p.Item, item));

    void Arrange() => AppTestHost.Arrange(this, User, Today);

    static ReferenceItem NewItem(string name)
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), ListId, name, 0),
            DateTimeOffset.UnixEpoch));
        return item;
    }

    static void AddField(ReferenceItem item, string label, string value) =>
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), label, value, null),
            DateTimeOffset.UnixEpoch));
}
