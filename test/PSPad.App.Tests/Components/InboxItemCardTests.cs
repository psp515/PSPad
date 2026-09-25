using Bunit;
using Microsoft.AspNetCore.Components;
using PSPad.App.Components;
using PSPad.Module.Tasks.Inbox;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxItemCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItIsItsOwnOutlinedCard()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");

        var card = Render<InboxItemCard>(parameters => parameters.Add(p => p.Item, item));

        var classes = card.Find(".mud-paper").ClassList;
        Assert.Contains("mud-paper-outlined", classes);
        Assert.Contains("Kupić mleko", card.Markup);
    }

    [Fact]
    public void ItShowsWhenTheItemWasCaptured()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");

        var card = Render<InboxItemCard>(parameters => parameters.Add(p => p.Item, item));

        Assert.Contains(item.CapturedAt.ToLocalTime().ToString("d MMM"), card.Find(".pspad-inbox-item-captured").TextContent);
    }

    [Fact]
    public void ClickingItRaisesOnOpenWithTheItem()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");
        InboxItem? opened = null;

        var card = Render<InboxItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnOpen, EventCallback.Factory.Create<InboxItem>(this, i => opened = i)));

        card.Find(".pspad-inbox-item").Click();

        Assert.Equal(item.Id, opened?.Id);
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
