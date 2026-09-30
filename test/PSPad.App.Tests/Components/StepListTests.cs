using Bunit;
using Microsoft.AspNetCore.Components.Web;
using PSPad.App.Components;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class StepListTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public async Task EnterInTheAddFieldAddsAStep()
    {
        var task = NewTask();
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-add input");
        field.Input("Check the fridge");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal("Check the fridge", Assert.Single(stored!.Steps).Name);
    }

    [Fact]
    public async Task ABlankStepIsNotAdded()
    {
        var task = NewTask();
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-add input");
        field.Input("   ");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Empty(stored!.Steps);
    }

    [Fact]
    public async Task RemovingAStepDropsIt()
    {
        var task = NewTask();
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Check the fridge"),
            DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find(".pspad-step-remove").Click();

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Empty(stored!.Steps);
    }

    [Fact]
    public async Task ADraftCollectsStepsInOrderWithoutSendingCommands()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<string>();

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-add input");
        field.Input("Check the fridge");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        field.Input("   ");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        field.Input("Go to the shop");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Check the fridge", "Go to the shop"], draft);
        Assert.Equal(["Check the fridge", "Go to the shop"],
            steps.FindAll(".pspad-step-name").Select(name => name.TextContent));
        Assert.Empty(steps.FindAll(".pspad-step-check"));
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void RemovingADraftStepDropsItFromTheDraft()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<string> { "Check the fridge", "Go to the shop" };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.FindAll(".pspad-step-remove")[0].Click();

        Assert.Equal(["Go to the shop"], draft);
        Assert.Single(steps.FindAll(".pspad-step-name"));
    }

    static TodoTask NewTask()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Buy milk"),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
