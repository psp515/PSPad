using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.Module.Tasks.Goals;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class GoalDetailPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoGoalItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<GoalDetailPanel>();

        Assert.Empty(panel.FindAll(".pspad-goal-name-field"));
    }

    [Fact]
    public void ANewGoalOffersTheNameAndAnAddButtonOnly()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<GoalDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));

        Assert.Contains("New goal", panel.Find(".pspad-panel-title").TextContent);
        Assert.Contains("Add goal", panel.Find(".pspad-panel-save").TextContent);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
        Assert.Empty(panel.FindAll(".pspad-goal-status"));
    }

    [Fact]
    public async Task AddingANewGoalCreatesItAndClosesThePanel()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/goals?goal=new");

        var panel = Render<GoalDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-goal-name-field input").Input("Learn to bake");
        panel.Find(".pspad-panel-save").Click();

        var goals = await replica.LoadAllAsync<Goal>(User);
        Assert.Contains(goals, goal => goal.Name == "Learn to bake");
        Assert.EndsWith("/goals", navigation.Uri);
    }

    [Fact]
    public async Task EnterAddsTheNewGoalButABlankNameAddsNothing()
    {
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = Render<GoalDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));
        var field = panel.Find(".pspad-goal-name-field input");
        field.Input("  ");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Empty(await replica.LoadAllAsync<Goal>(User));

        panel.Find(".pspad-goal-name-field input").Input("Learn to bake");
        panel.Find(".pspad-goal-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Single(await replica.LoadAllAsync<Goal>(User));
    }

    [Fact]
    public async Task AnExistingGoalRenamesLiveWithoutASaveButton()
    {
        var goal = NewGoal("Eat healthier");
        var replica = AppTestHost.Arrange(this, User, Today, goal);

        var panel = Render<GoalDetailPanel>(parameters => parameters.Add(p => p.GoalId, (Guid?)goal.Id));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        panel.Find(".pspad-goal-name-field input").Change("Eat better");

        var stored = await replica.LoadAsync<Goal>(goal.Id);
        Assert.Equal("Eat better", stored!.Name);
    }

    [Fact]
    public void TheStatusSelectorShowsTheCurrentStatus()
    {
        var goal = NewGoal("Eat healthier");
        AppTestHost.Arrange(this, User, Today, goal);

        var panel = Render<GoalDetailPanel>(parameters => parameters.Add(p => p.GoalId, (Guid?)goal.Id));

        Assert.Equal("In progress", panel.Find(".pspad-goal-status input").GetAttribute("value"));
    }

    [Theory]
    [InlineData(GoalStatus.Achieved)]
    [InlineData(GoalStatus.NotAchieved)]
    public async Task PickingAStatusSavesIt(GoalStatus status)
    {
        var goal = NewGoal("Eat healthier");
        var replica = AppTestHost.Arrange(this, User, Today, goal);

        var panel = RenderWithOverlays(goal.Id);
        panel.Find(".pspad-goal-status .mud-select-input").MouseDown();
        panel.FindAll(".mud-list-item")[(int)status].Click();

        Assert.Equal(status, (await replica.LoadAsync<Goal>(goal.Id))!.Status);
    }

    [Fact]
    public async Task PickingADueDateSavesItAndClearingRemovesIt()
    {
        var goal = NewGoal("Eat healthier");
        var replica = AppTestHost.Arrange(this, User, Today, goal);

        var panel = RenderWithOverlays(goal.Id);
        panel.Find(".pspad-task-due .pspad-property-activator").Click();
        panel.FindAll(".pspad-due-quick")[1].Click();
        Assert.Equal(Today.AddDays(1), (await replica.LoadAsync<Goal>(goal.Id))!.DueOn);

        panel.WaitForAssertion(() => panel.Find(".pspad-property-clear").Click());
        Assert.Null((await replica.LoadAsync<Goal>(goal.Id))!.DueOn);
    }

    [Fact]
    public async Task ANewGoalKeepsTheDueDatePickedBeforeAdding()
    {
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<GoalDetailPanel>(1);
            builder.AddAttribute(2, nameof(GoalDetailPanel.IsNew), true);
            builder.CloseComponent();
        });
        panel.Find(".pspad-task-due .pspad-property-activator").Click();
        panel.FindAll(".pspad-due-quick")[2].Click();
        panel.Find(".pspad-goal-name-field input").Input("Learn to bake");
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<Goal>(User));
        Assert.Equal(Today.AddDays(2), created.DueOn);
    }

    [Fact]
    public async Task DeletingAGoalAsksFirstThenClosesThePanel()
    {
        var goal = NewGoal("Eat healthier");
        var replica = AppTestHost.Arrange(this, User, Today, goal);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/goals?goal={goal.Id}");

        var panel = RenderWithOverlays(goal.Id);
        Assert.Contains("Delete goal", panel.Find(".pspad-panel-delete").TextContent);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        Assert.True((await replica.LoadAsync<Goal>(goal.Id))!.Deleted);
        Assert.EndsWith("/goals", navigation.Uri);
    }

    [Fact]
    public async Task CancellingTheDeleteKeepsTheGoal()
    {
        var goal = NewGoal("Eat healthier");
        var replica = AppTestHost.Arrange(this, User, Today, goal);

        var panel = RenderWithOverlays(goal.Id);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").First().Click();

        Assert.False((await replica.LoadAsync<Goal>(goal.Id))!.Deleted);
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        AppTestHost.Arrange(this, User, Today);
        var closed = false;

        var panel = Render<GoalDetailPanel>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-panel-close").Click();

        Assert.True(closed);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid goalId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<GoalDetailPanel>(2);
        builder.AddAttribute(3, nameof(GoalDetailPanel.GoalId), (Guid?)goalId);
        builder.CloseComponent();
    });

    static Goal NewGoal(string name)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return goal;
    }
}
