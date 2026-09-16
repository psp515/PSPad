using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ListCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItShowsAtMostTenOpenTasks()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 17));

        Assert.Equal(10, card.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void ShowAllAppearsOnlyWhenThereAreMoreThanTen()
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
    public void TheHeaderCountsOpenTasksNotRenderedRows()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 17));

        Assert.Equal("17", card.Find(".pspad-open-count").TextContent);
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
    public void ACollapsedCardRendersNoRows()
    {
        var collapse = Arrange();
        var list = List("Remont");
        Toggle(collapse, list.Id);

        var card = Render(list, Tasks(list.Id, 3));

        Assert.Empty(card.FindComponents<TaskRow>());
        Assert.Contains("Remont", card.Markup);
    }

    [Fact]
    public void TheCardHasNoDefaultElevationShadowSoItDoesNotDoubleUpWithTheGridHairline()
    {
        Arrange();
        var list = List("Remont");

        var card = Render(list, Tasks(list.Id, 1));

        Assert.Contains("mud-elevation-0", card.Find(".mud-paper").ClassList);
    }

    [Fact]
    public void TheHeaderCarriesAMenuThatRaisesRenameAndDelete()
    {
        Arrange();
        var list = List("Zakupy");
        var renamed = false;
        var deleted = false;

        var card = Render(BuildCardWithPopover(list, () => renamed = true, () => deleted = true));

        card.Find(".pspad-list-menu button").Click();
        card.FindAll(".mud-menu-item")[0].Click();
        Assert.True(renamed);

        card.Find(".pspad-list-menu button").Click();
        card.FindAll(".mud-menu-item").Last().Click();
        Assert.True(deleted);
    }

    IRenderedComponent<ListCard> Render(TaskList list, IReadOnlyList<TodoTask> tasks) =>
        Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.Tasks, tasks)
            .Add(p => p.Today, Today));

    // MudMenu portals its open content through MudPopoverProvider, so this render
    // tree needs one alongside ListCard for the menu item clicks to be reachable.
    RenderFragment BuildCardWithPopover(TaskList list, Action onRename, Action onDelete) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<ListCard>(1);
        builder.AddAttribute(2, nameof(ListCard.List), list);
        builder.AddAttribute(3, nameof(ListCard.Today), Today);
        builder.AddAttribute(4, nameof(ListCard.OnRename), new EventCallback(null, onRename));
        builder.AddAttribute(5, nameof(ListCard.OnDelete), new EventCallback(null, onDelete));
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
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
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
