using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxItemPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoItemItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, (InboxItem?)null)
            .Add(p => p.InboxId, Guid.NewGuid()));

        Assert.DoesNotContain("Move", panel.Markup);
    }

    [Fact]
    public void WithAnItemItShowsItsTextAndTheFirstAreaAndListByDefault()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox, area, list);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.Contains("Kupić mleko", panel.Markup);
        Assert.Contains("Dom", panel.Markup);
    }

    [Fact]
    public async Task MovingCreatesTheTaskInTheChosenListAndOrganisesTheItem()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        var replica = AppTestHost.Arrange(this, User, Today, inbox, area, list);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));
        panel.Find("button.pspad-inbox-move").Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        Assert.Contains(tasks, task => task.ListId == list.Id && task.Name == "Kupić mleko");

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
    }

    [Fact]
    public async Task DiscardingRemovesTheItemAndCreatesNoTask()
    {
        var inbox = NewInbox("Nieaktualne");
        var replica = AppTestHost.Arrange(this, User, Today, inbox);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));
        panel.Find("button.pspad-inbox-discard").Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox);
        var closed = false;

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-inbox-panel-close").Click();

        Assert.True(closed);
    }

    [Fact]
    public void WithNoListsInTheAreaTheMoveButtonIsDisabledRatherThanAbsent()
    {
        var area = NewArea("Dom", 0);
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox, area);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.True(panel.Find("button.pspad-inbox-move").HasAttribute("disabled"));
        Assert.Contains("No lists in this area", panel.Markup);
    }

    [Fact]
    public void OnASmallViewportTheDrawerFillsTheFullWidthInsteadOfAFixedColumn()
    {
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox);
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.Contains("100%", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    static Inbox NewInbox(params string[] texts)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));

        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(
                inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
                DateTimeOffset.UnixEpoch));
        }

        return inbox;
    }

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
