using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Replica;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class GoalsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItNamesEachGoal()
    {
        Arrange(NewGoal("Eat healthier"), NewGoal("Ship the redesign"));

        var page = Render<GoalsPage>();

        Assert.Contains("Eat healthier", page.Markup);
        Assert.Contains("Ship the redesign", page.Markup);
    }

    [Fact]
    public void ItShowsNoGoalsYetWhenThereAreNone()
    {
        Arrange();

        var page = Render<GoalsPage>();

        Assert.Contains("No goals yet.", page.Markup);
    }

    [Fact]
    public void ItLaysGoalCardsOutInTheGrid()
    {
        Arrange(NewGoal("Eat healthier"));

        var page = Render<GoalsPage>();

        page.Find(".mud-grid");
    }

    [Fact]
    public void ItLaysAchievedGoalCardsOutInTheGrid()
    {
        Arrange(NewGoal("Eat healthier", achieved: true));

        var page = Render<GoalsPage>();
        var grids = page.FindAll(".mud-grid");

        Assert.Contains(grids, grid => grid.TextContent.Contains("Eat healthier"));
    }

    [Fact]
    public void ItShowsCardSkeletonsBeforeItHasLoaded()
    {
        ArrangeWithPendingStore();

        var page = Render<GoalsPage>();

        Assert.Single(page.FindComponents<CardSkeleton>());
        Assert.DoesNotContain("No goals yet.", page.Markup);
    }

    [Fact]
    public void EachGoalIsACard()
    {
        Arrange(NewGoal("Eat healthier"), NewGoal("Ship the redesign"));

        var page = Render<GoalsPage>();

        Assert.Equal(2, page.FindComponents<GoalCard>().Count);
    }

    [Fact]
    public void ACardHasNoDefaultElevationShadowSoItDoesNotDoubleUpWithTheGridHairline()
    {
        Arrange(NewGoal("Eat healthier"));

        var page = Render<GoalsPage>();
        var card = page.FindComponents<GoalCard>().Single();

        Assert.Contains("mud-elevation-0", card.Find(".mud-paper").ClassList);
    }

    [Fact]
    public void ACardShowsOnlyItsOwnOpenTasks()
    {
        var goal = NewGoal("Eat healthier");
        var other = NewGoal("Ship the redesign");
        var list = NewList("Health");
        Arrange(goal, other, list, NewTask(list.Id, "Book a check-up", goal.Id), NewTask(list.Id, "Deploy", other.Id));

        var page = Render<GoalsPage>();
        var card = page.FindComponents<GoalCard>().Single(c => c.Instance.Goal.Id == goal.Id);

        Assert.Contains("Book a check-up", card.Markup);
        Assert.DoesNotContain("Deploy", card.Markup);
    }

    [Fact]
    public void ATasksListNameIsShownOnItsRow()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        Arrange(goal, list, NewTask(list.Id, "Book a check-up", goal.Id));

        var page = Render<GoalsPage>();

        Assert.Contains("Health", page.Markup);
    }

    [Fact]
    public async Task AchievingAGoalMovesItToTheAchievedSection()
    {
        var goal = NewGoal("Eat healthier");
        var replica = Arrange(goal);

        var page = Render(BuildGoalsPageWithPopovers());
        page.Find(".pspad-goal-menu button").Click();
        page.FindAll(".mud-menu-item").First(item => item.TextContent.Trim() == "Achieve").Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.True(stored!.Achieved);
        Assert.Contains("Achieved (1)", page.Markup);
    }

    [Fact]
    public async Task ReopeningAnAchievedGoalMovesItBack()
    {
        var goal = NewGoal("Eat healthier", achieved: true);
        var replica = Arrange(goal);

        var page = Render(BuildGoalsPageWithPopovers());
        page.Find(".pspad-goal-menu button").Click();
        page.FindAll(".mud-menu-item").First(item => item.TextContent.Trim() == "Reopen").Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.False(stored!.Achieved);
    }

    [Fact]
    public async Task DeletingAGoalFromItsCardRemovesItFromTheScreen()
    {
        var goal = NewGoal("Eat healthier");
        var replica = Arrange(goal);

        var page = Render(BuildGoalsPageWithPopovers());
        page.Find(".pspad-goal-menu button").Click();
        page.FindAll(".mud-menu-item").Last().Click();

        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.FindAll("button").Last().Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.True(stored!.Deleted);
    }

    [Fact]
    public async Task TypingIntoTheNewGoalFieldCreatesItInTheReplica()
    {
        var replica = Arrange();

        var page = Render<GoalsPage>();
        var input = page.Find("input[placeholder='New goal']");
        input.Input("Learn to bake");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var goals = await replica.LoadAllAsync<Goal>(User);
        Assert.Contains(goals, goal => goal.Name == "Learn to bake");
    }

    [Fact]
    public void ClickingATaskSetsTheTaskQueryString()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        var task = NewTask(list.Id, "Book a check-up", goal.Id);
        Arrange(goal, list, task);

        var page = Render<GoalsPage>();
        page.Find(".pspad-task-name").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"?task={task.Id}", navigation.Uri);
    }

    // ThingMenu's MudMenu and IDialogService's MudDialogProvider both portal their open
    // content through MudPopoverProvider, so all three must share one render tree.
    RenderFragment BuildGoalsPageWithPopovers() => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<GoalsPage>(2);
        builder.CloseComponent();
    };

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12), documents);

    void ArrangeWithPendingStore()
    {
        Arrange();
        Services.AddSingleton<IDocumentStore<Goal>>(new NeverLoadingGoalStore());
    }

    sealed class NeverLoadingGoalStore : IDocumentStore<Goal>
    {
        public Task<Goal?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<Goal?>().Task;

        public Task<IReadOnlyList<Goal>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<Goal>>().Task;
    }

    static Goal NewGoal(string name, bool achieved = false)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));

        if (achieved)
        {
            goal.ApplyAll(Goal.Decide(goal, new AchieveGoal(Guid.NewGuid(), User, goal.Id), DateTimeOffset.UnixEpoch));
        }

        return goal;
    }

    static TaskList NewList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask NewTask(Guid listId, string name, Guid goalId)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name), DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(
            task, new LinkTaskToGoal(Guid.NewGuid(), User, task.Id, goalId), DateTimeOffset.UnixEpoch));
        return task;
    }
}
