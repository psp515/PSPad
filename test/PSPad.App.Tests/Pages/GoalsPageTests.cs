using Bunit;
using Microsoft.AspNetCore.Components;
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
    public void WithNoGoalsTheEmptyStateOpensTheNewGoalPanel()
    {
        Arrange();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/goals");

        var page = Render<GoalsPage>();
        var empty = page.FindComponent<EmptyState>();
        Assert.Contains("No goals yet.", empty.Markup);
        empty.Find(".pspad-empty-state").Click();

        Assert.EndsWith("/goals?goal=new", navigation.Uri);
    }

    [Fact]
    public void WithGoalsThereIsNoEmptyState()
    {
        Arrange(NewGoal("Eat healthier"));

        var page = Render<GoalsPage>();

        Assert.Empty(page.FindComponents<EmptyState>());
    }

    [Fact]
    public void ClickingAGoalsNameOpensItsPanel()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/goals");

        var page = Render<GoalsPage>();
        page.Find(".pspad-goal-name").Click();

        Assert.EndsWith($"/goals?goal={goal.Id}", navigation.Uri);
    }

    [Fact]
    public async Task AGoalSentFromElsewhereShowsUpWithoutAReload()
    {
        Arrange();

        var page = Render<GoalsPage>();
        var sender = Services.GetRequiredService<PSPad.App.State.Dispatch.CommandSender>();
        await page.InvokeAsync(() => sender.SendAsync(
            new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), "Learn to bake")));

        page.WaitForAssertion(() => Assert.Contains("Learn to bake", page.Markup));
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
    public void ACardIsOutlinedWithNoShadowLikeAListCard()
    {
        Arrange(NewGoal("Eat healthier"));

        var page = Render<GoalsPage>();
        var card = page.FindComponents<GoalCard>().Single();

        var paper = card.Find(".mud-paper").ClassList;
        Assert.Contains("mud-paper-outlined", paper);
        Assert.DoesNotContain(paper, name => name.StartsWith("mud-elevation-") && name != "mud-elevation-0");
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
        page.FindAll(".mud-menu-item").First(item => item.TextContent.Trim() == "Mark achieved").Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.True(stored!.Achieved);
        page.WaitForAssertion(() => Assert.Contains("Eat healthier",
            page.Find(".pspad-goals-achieved .pspad-goal-summary").TextContent));
    }

    [Fact]
    public void AnInProgressCardsMenuOffersTheTwoClosingStatuses()
    {
        Arrange(NewGoal("Eat healthier"));

        var page = Render(BuildGoalsPageWithPopovers());
        page.Find(".pspad-goal-menu button").Click();

        var options = page.FindAll(".pspad-goal-status-option").Select(item => item.TextContent.Trim());
        Assert.Equal(["Mark achieved", "Mark not achieved"], options);
    }

    [Fact]
    public async Task MarkingAGoalNotAchievedMovesItToItsOwnSection()
    {
        var goal = NewGoal("Eat healthier");
        var replica = Arrange(goal);

        var page = Render(BuildGoalsPageWithPopovers());
        page.Find(".pspad-goal-menu button").Click();
        page.FindAll(".mud-menu-item").First(item => item.TextContent.Trim() == "Mark not achieved").Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.Equal(GoalStatus.NotAchieved, stored!.Status);
        page.WaitForAssertion(() => Assert.Contains("Eat healthier",
            page.Find(".pspad-goals-not-achieved .pspad-goal-summary").TextContent));
    }

    [Fact]
    public void InProgressGoalsAreOrderedByDueDateWithUndatedLast()
    {
        var undated = NewGoal("Aaa undated");
        var later = NewGoal("Bbb later", dueOn: new DateOnly(2026, 12, 1));
        var sooner = NewGoal("Ccc sooner", dueOn: new DateOnly(2026, 10, 1));
        Arrange(undated, later, sooner);

        var page = Render<GoalsPage>();
        var names = page.FindAll(".pspad-goal-name").Select(name => name.TextContent.Trim()).ToArray();

        Assert.Equal(["Ccc sooner", "Bbb later", "Aaa undated"], names);
    }

    [Fact]
    public void ACardsDueDateSitsOnItsOwnLineUnderTheName()
    {
        Arrange(NewGoal("Ship PSPad v1", dueOn: new DateOnly(2026, 9, 27)));

        var page = Render<GoalsPage>();
        var name = page.Find(".pspad-goal-name");

        Assert.Same(name.ParentElement, page.Find(".pspad-goal-due").ParentElement);
        Assert.Contains("flex-column", name.ParentElement!.ClassName);
    }

    [Fact]
    public void AnOverdueGoalShowsItsDueDateInTheErrorColour()
    {
        Arrange(NewGoal("Eat healthier", dueOn: new DateOnly(2026, 9, 1)));

        var page = Render<GoalsPage>();
        var due = page.Find(".pspad-goal-due");

        Assert.Contains("Due Tue, 1 Sep", due.TextContent);
        Assert.Contains("mud-error-text", due.ClassName);
    }

    [Fact]
    public void ClosedGoalsShowAsSummariesInTheirOwnSectionsNotAsCards()
    {
        var active = NewGoal("Eat healthier");
        var achieved = NewGoal("Run a marathon", achieved: true);
        var list = NewList("Health");
        Arrange(active, achieved, list, NewTask(list.Id, "Buy shoes", achieved.Id));

        var page = Render<GoalsPage>();

        Assert.Single(page.FindComponents<GoalCard>());
        var summary = page.Find(".pspad-goals-achieved .pspad-goal-summary");
        Assert.Contains("Run a marathon", summary.TextContent);
        Assert.Contains("0 of 1 task done", summary.TextContent);
        Assert.Contains("1", page.Find(".pspad-goals-achieved .pspad-goals-section-count").TextContent);
        Assert.Empty(page.FindAll(".pspad-goals-not-achieved"));
        Assert.Empty(page.FindAll(".mud-expand-panel"));
    }

    [Fact]
    public void ClickingASummaryOpensTheGoalPanel()
    {
        var achieved = NewGoal("Run a marathon", achieved: true);
        Arrange(achieved);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/goals");

        var page = Render<GoalsPage>();
        page.Find(".pspad-goal-summary").Click();

        Assert.EndsWith($"/goals?goal={achieved.Id}", navigation.Uri);
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
    public void ClickingTheFabOpensTheNewGoalPanel()
    {
        Arrange();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/goals");

        var page = Render<GoalsPage>();
        var fab = page.Find(".pspad-fab");
        Assert.Equal("Add goal", fab.GetAttribute("aria-label"));
        fab.Click();

        Assert.EndsWith("/goals?goal=new", navigation.Uri);
    }

    [Fact]
    public void TheInlineNewGoalFieldIsGone()
    {
        Arrange();

        var page = Render<GoalsPage>();

        Assert.Empty(page.FindAll("input[placeholder='New goal']"));
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

    static Goal NewGoal(string name, bool achieved = false, DateOnly? dueOn = null)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));

        if (achieved)
        {
            goal.ApplyAll(Goal.Decide(goal, new AchieveGoal(Guid.NewGuid(), User, goal.Id), DateTimeOffset.UnixEpoch));
        }

        if (dueOn is not null)
        {
            goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, dueOn), DateTimeOffset.UnixEpoch));
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
