using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class GoalPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItNamesTheGoalAndShowsEveryOpenTask()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        Arrange([goal, list, .. Enumerable.Range(0, 7).Select(index => NewTask(list.Id, $"Task {index}", goal.Id))]);

        var page = RenderPage(goal.Id);

        Assert.Contains("Eat healthier", page.Find("h5").TextContent);
        Assert.Equal(7, page.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void TasksOfOtherGoalsAreNotThere()
    {
        var goal = NewGoal("Eat healthier");
        var other = NewGoal("Ship it");
        var list = NewList("Health");
        Arrange(goal, other, list, NewTask(list.Id, "Book a check-up", goal.Id), NewTask(list.Id, "Deploy", other.Id));

        var page = RenderPage(goal.Id);

        Assert.DoesNotContain("Deploy", page.Markup);
    }

    [Fact]
    public void EachRowNamesItsListAndTheDayItWasAdded()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        Arrange(goal, list, NewTask(list.Id, "Book a check-up", goal.Id));

        var row = RenderPage(goal.Id).FindComponent<TaskRow>();

        Assert.Equal("Health", row.Instance.ListName);
        Assert.Equal("Added 1 Jan", row.Find(".pspad-created").TextContent.Trim());
    }

    [Fact]
    public void EachTaskIsItsOwnCardInTheGrid()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        Arrange(goal, list, NewTask(list.Id, "A", goal.Id), NewTask(list.Id, "B", goal.Id));

        var page = RenderPage(goal.Id);

        page.Find(".mud-grid");
        Assert.Equal(2, page.FindAll(".pspad-task-card").Count);
    }

    [Fact]
    public void CompletedTasksAreCountedInTheirOwnSection()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        var done = NewTask(list.Id, "Buy oats", goal.Id);
        done.ApplyAll(TodoTask.Decide(done, new CompleteTask(Guid.NewGuid(), User, done.Id), DateTimeOffset.UnixEpoch));
        Arrange(goal, list, NewTask(list.Id, "Book a check-up", goal.Id), done);

        var page = RenderPage(goal.Id);

        Assert.Contains("Completed (1)", page.Markup);
    }

    [Fact]
    public void WithNoOpenTasksItSaysSo()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);

        var page = RenderPage(goal.Id);

        Assert.Contains("No open tasks.", page.FindComponent<EmptyState>().Markup);
    }

    [Fact]
    public void ItShowsTheDueDateUnderTheTitle()
    {
        var goal = NewGoal("Eat healthier", dueOn: new DateOnly(2026, 9, 1));
        Arrange(goal);

        var due = RenderPage(goal.Id).Find(".pspad-goal-due");

        Assert.Contains("Due Tue, 1 Sep", due.TextContent);
        Assert.Contains("mud-error-text", due.ClassName);
    }

    [Fact]
    public void TheBackButtonLeadsToGoals()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);

        var back = RenderPage(goal.Id).Find(".pspad-back-to-goals");

        Assert.Equal("/goals", back.GetAttribute("href"));
    }

    [Fact]
    public void ADeletedGoalShowsItIsGone()
    {
        var goal = NewGoal("Eat healthier");
        goal.ApplyAll(Goal.Decide(goal, new DeleteGoal(Guid.NewGuid(), User, goal.Id), DateTimeOffset.UnixEpoch));
        Arrange(goal);

        var page = RenderPage(goal.Id);

        Assert.Contains("This goal is not there anymore.", page.Markup);
    }

    [Fact]
    public void ItShowsASkeletonAndNoVerdictBeforeItHasLoaded()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);
        Services.AddSingleton<IDocumentStore<Goal>>(new NeverLoadingGoalStore());

        var page = RenderPage(goal.Id);

        Assert.Single(page.FindComponents<RowSkeleton>());
        Assert.DoesNotContain("not there anymore", page.Markup);
    }

    [Fact]
    public void ClickingATaskOpensItOnThisPage()
    {
        var goal = NewGoal("Eat healthier");
        var list = NewList("Health");
        var task = NewTask(list.Id, "Book a check-up", goal.Id);
        Arrange(goal, list, task);

        var page = RenderPage(goal.Id);
        page.Find(".pspad-task-name").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/goals/{goal.Id}?task={task.Id}", navigation.Uri);
    }

    [Fact]
    public void EditGoalFromTheFabMenuOpensTheGoalPanel()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/goals/{goal.Id}");

        var page = Render(BuildWithDialogs(goal.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[0].Click();

        Assert.EndsWith($"/goals/{goal.Id}?goal={goal.Id}", navigation.Uri);
    }

    [Fact]
    public async Task DeleteGoalFromTheFabMenuDeletesAndGoesBackToGoals()
    {
        var goal = NewGoal("Eat healthier");
        var replica = Arrange(goal);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/goals/{goal.Id}");

        var page = Render(BuildWithDialogs(goal.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();
        page.FindAll(".mud-fab-menu-item")[1].Click();
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.True(stored!.Deleted);
        Assert.EndsWith("/goals", navigation.Uri);
    }

    [Fact]
    public void TheFabMenuItemsCarryAccessibleNames()
    {
        var goal = NewGoal("Eat healthier");
        Arrange(goal);

        var page = Render(BuildWithDialogs(goal.Id));
        page.Find(".pspad-fab .mud-fab-menu-button").Click();

        var labels = page.FindAll(".mud-fab-menu-item").Select(item => item.GetAttribute("aria-label"));
        Assert.Equal(["Edit goal", "Delete goal"], labels);
    }

    IRenderedComponent<GoalPage> RenderPage(Guid goalId) =>
        Render<GoalPage>(parameters => parameters.Add(p => p.GoalId, goalId));

    // MudFabMenu and IDialogService's MudDialogProvider both portal through
    // MudPopoverProvider, so all three must share one render tree.
    static RenderFragment BuildWithDialogs(Guid goalId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<GoalPage>(2);
        builder.AddAttribute(3, nameof(GoalPage.GoalId), goalId);
        builder.CloseComponent();
    };

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, Today, documents);

    sealed class NeverLoadingGoalStore : IDocumentStore<Goal>
    {
        public Task<Goal?> LoadAsync(Guid id, CancellationToken ct) => new TaskCompletionSource<Goal?>().Task;

        public Task<IReadOnlyList<Goal>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<Goal>>().Task;
    }

    static Goal NewGoal(string name, DateOnly? dueOn = null)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));

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
