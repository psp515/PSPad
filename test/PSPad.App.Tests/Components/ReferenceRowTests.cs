using Bunit;
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

        Assert.Contains("Bigos", row.Find(".mud-typography-body2").TextContent);
    }

    [Fact]
    public void TheCaptionShowsTheFirstTwoFieldsAsLabelValuePairs()
    {
        Arrange();
        var item = NewItem("Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");

        var row = Render(item);

        Assert.Contains("Time: 45 min · Servings: 4", row.Markup);
    }

    [Fact]
    public void AThirdFieldNeverShowsInTheCaption()
    {
        Arrange();
        var item = NewItem("Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AddField(item, "Difficulty", "Easy");

        var row = Render(item);

        Assert.DoesNotContain("Difficulty", row.Markup);
    }

    [Fact]
    public void QuantitiesAreNormalisedInTheCaption()
    {
        Arrange();
        var item = NewItem("Filament");
        AddField(item, "Weight", "1000g");

        var row = Render(item);

        Assert.Contains("Weight: 1000 g", row.Markup);
    }

    [Fact]
    public void LinksAndPathsShowAsPlainTextInTheCaption()
    {
        Arrange();
        var item = NewItem("Manual");
        AddField(item, "Docs", "https://example.com/manual");

        var row = Render(item);

        Assert.Contains("Docs: https://example.com/manual", row.Markup);
        Assert.Empty(row.FindAll("a"));
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
        row.Find("button").Click();

        Assert.Same(item, starred);
    }

    [Fact]
    public void TheStarButtonIsLabelledStarWhenNotStarred()
    {
        Arrange();
        var item = NewItem("Bigos");

        var row = Render(item);

        Assert.Equal("Star", row.Find("button").GetAttribute("aria-label"));
    }

    [Fact]
    public void TheStarButtonIsLabelledUnstarWhenStarred()
    {
        Arrange();
        var item = NewItem("Bigos");
        item.ApplyAll(ReferenceItem.Decide(
            item, new StarReferenceItem(Guid.NewGuid(), User, item.Id, true), DateTimeOffset.UnixEpoch));

        var row = Render(item);

        Assert.Equal("Unstar", row.Find("button").GetAttribute("aria-label"));
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
