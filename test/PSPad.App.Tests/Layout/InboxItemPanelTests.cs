using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class InboxItemPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNothingOpenItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today, NewInbox());

        var panel = Render<InboxItemPanel>();

        Assert.Empty(panel.FindAll(".pspad-inbox-name-field"));
    }

    [Fact]
    public void CapturingAsksOnlyForTheName()
    {
        AppTestHost.Arrange(this, User, Today, NewInbox());

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.IsNew, true));

        Assert.Contains("Capture", panel.Find(".pspad-panel-title").TextContent);
        panel.Find(".pspad-inbox-name-field");
        Assert.Empty(panel.FindAll(".pspad-task-list"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task AddingCapturesTheItemWithItsTimeAndCloses()
    {
        var inbox = NewInbox();
        var replica = AppTestHost.Arrange(this, User, Today, inbox);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/inbox?inbox=new");

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-inbox-name-field input").Input("Kupić mleko");
        panel.Find(".pspad-panel-save").Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        var item = Assert.Single(stored!.Items);
        Assert.Equal("Kupić mleko", item.Text);
        Assert.NotEqual(default, item.CapturedAt);
        Assert.EndsWith("/inbox", navigation.Uri);
    }

    [Fact]
    public async Task EnterCapturesButBlankTextDoesNot()
    {
        var inbox = NewInbox();
        var replica = AppTestHost.Arrange(this, User, Today, inbox);

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-inbox-name-field input").Input("   ");
        panel.Find(".pspad-inbox-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Empty((await replica.LoadAsync<Inbox>(inbox.Id))!.Items);

        panel.Find(".pspad-inbox-name-field input").Input("Kupić mleko");
        panel.Find(".pspad-inbox-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Single((await replica.LoadAsync<Inbox>(inbox.Id))!.Items);
    }

    [Fact]
    public void OpeningAnItemOffersTheTaskFieldsWithTheFirstListPicked()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox, area, list);

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)inbox.Items[0].Id));

        Assert.Equal("Kupić mleko", panel.Find(".pspad-inbox-name-field input").GetAttribute("value"));
        Assert.Contains("Dom › Zakupy", panel.Find(".pspad-task-list").TextContent);
        panel.Find(".pspad-task-due");
        panel.Find(".pspad-task-priority");
        panel.Find(".pspad-task-goal");
        panel.Find(".pspad-inbox-star");
        Assert.Contains("Captured", panel.Find(".pspad-inbox-captured").TextContent);
        Assert.Contains("Convert to task", panel.Find(".pspad-panel-save").TextContent);
        Assert.Contains("Discard", panel.Find(".pspad-panel-delete").TextContent);
    }

    [Fact]
    public async Task ConvertingCreatesTheFilledInTaskInOneStepAndEmptiesTheItem()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy");
        var repairs = NewList(area.Id, "Remont");
        var goal = NewGoal("Zdrowie");
        var inbox = NewInbox("Kupić mleko");
        var replica = AppTestHost.Arrange(this, User, Today, inbox, area, shopping, repairs, goal);

        var panel = RenderWithOverlays(inbox.Items[0].Id);
        panel.Find(".pspad-inbox-name-field input").Input("Kupić mleko owsiane");
        OpenRow(panel, ".pspad-task-list");
        panel.FindAll(".pspad-list-option")[1].Click();
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[1].Click();
        OpenRow(panel, ".pspad-task-priority");
        panel.FindAll(".pspad-priority-option")[(int)Priority.High].Click();
        OpenRow(panel, ".pspad-task-goal");
        panel.Find(".pspad-goal-option").Click();
        panel.Find(".pspad-inbox-star").Click();
        panel.Find(".pspad-panel-save").Click();

        var task = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal("Kupić mleko owsiane", task.Name);
        Assert.Equal(repairs.Id, task.ListId);
        Assert.Equal(Today.AddDays(1), task.DueOn);
        Assert.Equal(Priority.High, task.Priority);
        Assert.Equal(goal.Id, task.GoalId);
        Assert.True(task.Starred);
        Assert.Empty((await replica.LoadAsync<Inbox>(inbox.Id))!.Items);
    }

    [Fact]
    public async Task TheNextItemKeepsTheListPickedForTheLastOne()
    {
        var area = NewArea("Dom");
        var shopping = NewList(area.Id, "Zakupy");
        var repairs = NewList(area.Id, "Remont");
        var inbox = NewInbox("Kupić mleko", "Kupić farbę");
        var replica = AppTestHost.Arrange(this, User, Today, inbox, area, shopping, repairs);

        var secondItemId = inbox.Items[1].Id;

        var first = RenderWithOverlays(inbox.Items[0].Id);
        OpenRow(first, ".pspad-task-list");
        first.FindAll(".pspad-list-option")[1].Click();
        first.Find(".pspad-panel-save").Click();

        Assert.Equal(repairs.Id, Services.GetRequiredService<AppState>().LastInboxListId);
        var second = Render<InboxItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)secondItemId));
        Assert.Contains("Dom › Remont", second.Find(".pspad-task-list").TextContent);
    }

    [Fact]
    public void WithNoListsConvertingIsDisabled()
    {
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox);

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)inbox.Items[0].Id));

        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task DiscardingRemovesTheItemAndCreatesNoTask()
    {
        var inbox = NewInbox("Kupić mleko");
        var replica = AppTestHost.Arrange(this, User, Today, inbox);

        var panel = Render<InboxItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)inbox.Items[0].Id));
        panel.Find(".pspad-panel-delete").Click();

        Assert.Empty((await replica.LoadAsync<Inbox>(inbox.Id))!.Items);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        AppTestHost.Arrange(this, User, Today, NewInbox());
        var closed = false;

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-panel-close").Click();

        Assert.True(closed);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid itemId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<InboxItemPanel>(1);
        builder.AddAttribute(2, nameof(InboxItemPanel.ItemId), (Guid?)itemId);
        builder.CloseComponent();
    });

    static void OpenRow(IRenderedComponent<ContainerFragment> panel, string row) =>
        panel.Find($"{row} .pspad-property-activator").Click();

    static Inbox NewInbox(params string[] texts)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));

        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
                DateTimeOffset.UnixEpoch.AddDays(1)));
        }

        return inbox;
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    int _position;

    TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, _position++),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static Goal NewGoal(string name)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return goal;
    }
}
