using Bunit;
using Microsoft.AspNetCore.Components;
using PSPad.App.Components;
using PSPad.Module.Tasks.Inbox;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxItemRowTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItIsARowAndShowsTheText()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");

        var row = Render<InboxItemRow>(parameters => parameters.Add(p => p.Item, item));

        var classes = row.Find(".pspad-inbox-item").ClassList;
        Assert.Contains("pspad-row", classes);
        Assert.Empty(row.FindAll(".mud-paper"));
        Assert.Contains("Kupić mleko", row.Markup);
    }

    [Fact]
    public void ItShowsTheCaptureTimeInTheGivenZone()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus5", TimeSpan.FromHours(5), "plus5", "plus5");

        var row = Render<InboxItemRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.Zone, zone));

        Assert.Equal("05:00", row.Find(".pspad-inbox-item-captured").TextContent.Trim());
    }

    [Fact]
    public void ClickingItRaisesOnOpenWithTheItem()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");
        InboxItem? opened = null;

        var row = Render<InboxItemRow>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnOpen, EventCallback.Factory.Create<InboxItem>(this, i => opened = i)));

        row.Find(".pspad-inbox-item").Click();

        Assert.Equal(item.Id, opened?.Id);
    }

    [Fact]
    public void TheFullTextRidesInTheTitleForALongCapture()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Zadzwonić do mechanika w sprawie opon");

        var row = Render<InboxItemRow>(parameters => parameters.Add(p => p.Item, item));

        Assert.Equal("Zadzwonić do mechanika w sprawie opon", row.Find(".pspad-inbox-item-text").GetAttribute("title"));
    }

    static InboxItem NewItem(string text)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));
        inbox.ApplyAll(Inbox.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
            DateTimeOffset.UnixEpoch));
        return inbox.Items[0];
    }
}
