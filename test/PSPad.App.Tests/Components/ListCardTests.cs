using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ListCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItShowsAtMostFiveOpenTasks()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 17));

        Assert.Equal(5, card.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void ShowAllAppearsOnlyWhenThereAreMoreThanFive()
    {
        Arrange();
        var list = List("Remont");

        var many = Render(list, Tasks(list.Id, 17));
        var few = Render(list, Tasks(list.Id, 4));

        Assert.Contains("Show all", many.Markup);
        Assert.Contains($"/lists/{list.Id}", many.Markup);
        Assert.DoesNotContain("Show all", few.Markup);
    }

    [Fact]
    public void CompletedTasksNeverAppearInACard()
    {
        Arrange();
        var list = List("Zakupy");
        var done = Tasks(list.Id, 1)[0];
        done.ApplyAll(TodoTask.Decide(
            done, new CompleteTask(Guid.NewGuid(), User, done.Id), DateTimeOffset.UnixEpoch));

        var card = Render(list, [done, .. Tasks(list.Id, 2)]);

        Assert.Equal(2, card.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void ARepeatPastItsUntilDateNeverAppearsInACard()
    {
        Arrange();
        var list = List("Regularne");
        var ended = Tasks(list.Id, 1)[0];
        ended.ApplyAll(TodoTask.Decide(ended,
            new SetTaskRecurrence(Guid.NewGuid(), User, ended.Id, RecurrenceRule.Daily(Today.AddDays(-7))),
            DateTimeOffset.UnixEpoch));
        ended.ApplyAll(TodoTask.Decide(ended,
            new SetTaskDueDate(Guid.NewGuid(), User, ended.Id, Today.AddDays(-1)), DateTimeOffset.UnixEpoch));

        var card = Render(list, [ended, .. Tasks(list.Id, 2)]);

        Assert.Equal(2, card.FindComponents<TaskRow>().Count);
        Assert.Equal("2 open", card.Find(".pspad-open-count").TextContent);
    }

    [Fact]
    public void TheHeaderCountsOpenTasksNotRenderedRows()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 17));

        Assert.Equal("17 open", card.Find(".pspad-open-count").TextContent);
    }

    [Fact]
    public void DeletedTasksNeverAppearInACard()
    {
        Arrange();
        var list = List("Remont");
        var pair = Tasks(list.Id, 2);
        var deleted = pair[0];
        var live = pair[1];
        deleted.ApplyAll(TodoTask.Decide(
            deleted, new DeleteTask(Guid.NewGuid(), User, deleted.Id), DateTimeOffset.UnixEpoch));

        var card = Render(list, [deleted, live]);

        Assert.DoesNotContain(deleted.Name, card.Markup);
        Assert.Contains(live.Name, card.Markup);
    }

    [Fact]
    public void ACollapsedCardFoldsItsRowsAway()
    {
        var collapse = Arrange();
        var list = List("Remont");
        Toggle(collapse, list.Id);

        var card = Render(list, Tasks(list.Id, 3));

        Assert.False(card.FindComponent<MudCollapse>().Instance.Expanded);
        Assert.Contains("Remont", card.Markup);
    }

    [Fact]
    public void TheCardIsOutlinedRatherThanElevated()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 1));

        var classes = card.Find(".mud-paper").ClassList;
        Assert.Contains("mud-paper-outlined", classes);
        Assert.DoesNotContain(classes, className => className.StartsWith("mud-elevation-", StringComparison.Ordinal)
            && className != "mud-elevation-0");
    }

    [Fact]
    public void TheHeaderCarriesAnAddTaskIconThatRaisesOnAddTaskClick()
    {
        Arrange();
        var list = List("Remont");
        var clicked = false;

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Tasks, Tasks(list.Id, 1))
            .Add(p => p.Today, Today)
            .Add(p => p.OnAddTaskClick, EventCallback.Factory.Create(this, () => clicked = true)));

        card.Find(".pspad-add-task").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void NoInlineAddTaskFieldRemainsInTheCard()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Empty(card.FindAll("input[placeholder='Add task']"));
    }

    [Fact]
    public void TheHeaderCarriesAMenuOfEditAndDeleteOnly()
    {
        Arrange();
        var list = List("Zakupy");
        var edited = false;
        var deleted = false;

        var card = Render(BuildCardWithPopover(list, () => edited = true, () => deleted = true));

        card.Find(".pspad-list-menu button").Click();
        Assert.Equal(["Edit", "Delete"], card.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim()));
        card.FindAll(".mud-menu-item")[0].Click();
        Assert.True(edited);

        card.Find(".pspad-list-menu button").Click();
        card.FindAll(".mud-menu-item").Last().Click();
        Assert.True(deleted);
    }

    [Fact]
    public void TheHeaderShowsASharedMarkerWithTheMemberCount()
    {
        Arrange();
        var list = SharedList("Zakupy", 1);

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Equal("Shared · 2 people", card.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void ASharedByLinkListWithNobodyJoinedUsesTheSingularPerson()
    {
        Arrange();
        var list = SharedList("Zakupy", 0);

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Equal("Shared · 1 person", card.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void AnExpiredInviteWithNobodyJoinedShowsNoSharedMarker()
    {
        Arrange();
        var list = SharedList("Zakupy", 0, Now.AddHours(-1));

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Empty(card.FindComponents<MudTooltip>());
    }

    [Fact]
    public void AnExpiredInviteWithMembersKeepsTheSharedMarker()
    {
        Arrange();
        var list = SharedList("Zakupy", 1, Now.AddHours(-1));

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Equal("Shared · 2 people", card.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void AnUnsharedListShowsNoSharedMarker()
    {
        Arrange();
        var list = List("Zakupy");

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Empty(card.FindComponents<MudTooltip>());
    }

    [Fact]
    public void TheMenuOffersNoDeleteWhenTheListIsNotMine()
    {
        Arrange();
        var list = List("Zakupy");

        var card = Render(BuildCardWithPopover(list, () => { }, () => { }, isMine: false));

        card.Find(".pspad-list-menu button").Click();
        Assert.Equal(["Edit"], card.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim()));
    }

    [Fact]
    public void AReferenceListsCardShowsItsItemsAsReferenceRows()
    {
        Arrange();
        var list = ReferenceList("Przepisy");

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Items, Items(list.Id, 7))
            .Add(p => p.Today, Today));

        Assert.Equal(5, card.FindComponents<ReferenceRow>().Count);
        Assert.Equal("7 open", card.Find(".pspad-open-count").TextContent);
    }

    [Fact]
    public void AReferenceListsAddButtonRaisesOnAddItemClick()
    {
        Arrange();
        var list = ReferenceList("Przepisy");
        var clicked = false;

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Today, Today)
            .Add(p => p.OnAddItemClick, EventCallback.Factory.Create(this, () => clicked = true)));
        card.Find(".pspad-add-item").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void TheAddItemButtonHasAnAriaLabel()
    {
        Arrange();
        var list = ReferenceList("Przepisy");

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Today, Today));

        Assert.Equal("Add item", card.Find(".pspad-add-item").GetAttribute("aria-label"));
    }

    [Fact]
    public void AReferenceListsRowsRaiseOnStarItemAndOnOpenItem()
    {
        Arrange();
        var list = ReferenceList("Przepisy");
        var item = Items(list.Id, 1)[0];
        ReferenceItem? starred = null;
        ReferenceItem? opened = null;

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Items, [item])
            .Add(p => p.Today, Today)
            .Add(p => p.OnStarItem, (ReferenceItem starredItem) => starred = starredItem)
            .Add(p => p.OnOpenItem, (ReferenceItem openedItem) => opened = openedItem));

        card.Find(".pspad-reference-name").Click();
        card.Find(".pspad-reference-row button").Click();

        Assert.Same(item, opened);
        Assert.Same(item, starred);
    }

    static TaskList ReferenceList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, ListKind.Reference),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static ReferenceItem[] Items(Guid listId, int count) =>
        [.. Enumerable.Range(0, count).Select(index =>
        {
            var item = new ReferenceItem();
            item.ApplyAll(ReferenceItem.Decide(
                null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), listId, $"Item {index}", index),
                DateTimeOffset.UnixEpoch));
            return item;
        })];

    IRenderedComponent<ListCard> Render(TaskList list, IReadOnlyList<TodoTask> tasks) =>
        Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Tasks, tasks)
            .Add(p => p.Today, Today));

    // MudMenu portals its open content through MudPopoverProvider, so this render
    // tree needs one alongside ListCard for the menu item clicks to be reachable.
    RenderFragment BuildCardWithPopover(TaskList list, Action onEdit, Action onDelete, bool isMine = true) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<ListCard>(1);
        builder.AddAttribute(2, nameof(ListCard.List), list);
        builder.AddAttribute(3, nameof(ListCard.Today), Today);
        builder.AddAttribute(4, nameof(ListCard.OnEdit), new EventCallback(null, onEdit));
        builder.AddAttribute(5, nameof(ListCard.OnDelete), new EventCallback(null, onDelete));
        builder.AddAttribute(6, nameof(ListCard.IsMine), isMine);
        builder.CloseComponent();
    };

    CardCollapseState Arrange()
    {
        AppTestHost.Arrange(this, User, Today);
        return Services.GetRequiredService<CardCollapseState>();
    }

    static void Toggle(CardCollapseState collapse, Guid listId) =>
        collapse.ToggleAsync(listId).GetAwaiter().GetResult();

    static TaskList List(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static readonly DateTimeOffset Now = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    static TaskList SharedList(string name, int memberCount) => SharedList(name, memberCount, Now);

    static TaskList SharedList(string name, int memberCount, DateTimeOffset sharedAt)
    {
        var list = List(name);
        list.Apply(new TaskListShared(list.Id, User, sharedAt, "k3Jv9s2mQ0x7b1nR4tYw8eZa", "Owner", "K7M4PX"));

        for (var index = 0; index < memberCount; index++)
        {
            list.Apply(new TaskListJoined(list.Id, User, DateTimeOffset.UnixEpoch, Guid.NewGuid(), $"Member {index}"));
        }

        return list;
    }

    static TodoTask[] Tasks(Guid listId, int count) =>
        [.. Enumerable.Range(0, count).Select(index =>
        {
            var task = new TodoTask();
            task.ApplyAll(TodoTask.Decide(
                null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, $"Task {index}"),
                DateTimeOffset.UnixEpoch));
            return task;
        })];
}
