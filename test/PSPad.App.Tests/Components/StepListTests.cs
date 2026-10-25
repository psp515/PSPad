using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Recurrence;
using PSPad.App.State.Outbox;
using PSPad.Abstractions;
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
    public void TheAddFieldStopsAtTheNameLimit()
    {
        var task = NewTask();
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));

        Assert.Equal($"{ContentLimits.Name}", steps.Find(".pspad-step-add input").GetAttribute("maxlength"));
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
    public void AStepMarkedOnASnapshotShowsTheMarkChip()
    {
        var task = NewTask();
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Check the fridge"),
            DateTimeOffset.UnixEpoch));
        var stepId = task.Steps.Single().Id;
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), User, task.Id, stepId, Guid.NewGuid(), true),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));

        Assert.Contains("pspad-snapshot-mark", steps.Markup);
    }

    [Fact]
    public void AStepWithoutAMarkShowsNoChip()
    {
        var task = NewTask();
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Check the fridge"),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));

        Assert.DoesNotContain("pspad-snapshot-mark", steps.Markup);
    }

    [Fact]
    public async Task ADraftCollectsStepsInOrderWithoutSendingCommands()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft>();

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-add input");
        field.Input("Check the fridge");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        field.Input("   ");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        field.Input("Go to the shop");
        field.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Check the fridge", "Go to the shop"], draft.Select(step => step.Name));
        Assert.Equal(["Check the fridge", "Go to the shop"],
            steps.FindAll(".pspad-step-name input").Select(name => name.GetAttribute("value")));
        Assert.Empty(steps.FindAll(".pspad-step-check"));
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void RemovingADraftStepDropsItFromTheDraft()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge"), new("Go to the shop") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.FindAll(".pspad-step-remove")[0].Click();

        Assert.Equal(["Go to the shop"], draft.Select(step => step.Name));
        Assert.Single(steps.FindAll(".pspad-step-name"));
    }

    [Fact]
    public async Task ChangingAStepNameSendsOneRenameStep()
    {
        var task = TaskWithStep("Check the fridge");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find(".pspad-step-name input").Change("  Check the freezer ");

        var sent = await AppTestHost.SentAsync<RenameStep>(this);
        Assert.Equal(task.Steps.Single().Id, sent.StepId);
        Assert.Equal("Check the freezer", sent.Name);
        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal("Check the freezer", stored!.Steps.Single().Name);
    }

    [Fact]
    public async Task AnUnchangedStepNameSendsNothing()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find(".pspad-step-name input").Change(" Check the fridge ");

        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
    }

    [Fact]
    public async Task AnEmptiedStepNameRevertsAndSendsNothing()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find(".pspad-step-name input").Change("   ");

        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Equal("Check the fridge", steps.Find(".pspad-step-name input").GetAttribute("value"));
    }

    [Fact]
    public async Task EscapeRevertsTheStepName()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-name input");
        var typedInto = field.Id;
        field.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        var restored = steps.Find(".pspad-step-name input");
        Assert.NotEqual(typedInto, restored.Id);
        Assert.Equal("Check the fridge", restored.GetAttribute("value"));
    }

    [Fact]
    public void TheStepNameFieldIsLabelledAndCapped()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var input = steps.Find(".pspad-step-name input");

        Assert.Equal("Rename Check the fridge", input.GetAttribute("aria-label"));
        Assert.Equal($"{ContentLimits.Name}", input.GetAttribute("maxlength"));
    }

    [Fact]
    public void ADisabledListCannotRenameAStep()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters
            .Add(p => p.Task, task).Add(p => p.UserId, User).Add(p => p.Disabled, true));

        Assert.True(steps.Find(".pspad-step-name input").HasAttribute("disabled"));
    }

    [Fact]
    public void ACheckedStepNameStaysStruckThrough()
    {
        var task = TaskWithStep("Check the fridge");
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, task.Steps.Single().Id, true), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        Assert.Contains("pspad-step-done", steps.Find(".pspad-step-name").ClassList);
    }

    [Fact]
    public void EditingADraftStepRewritesItInPlace()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge"), new("Go to the shop") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.FindAll(".pspad-step-name input")[1].Change(" Go to the market ");

        Assert.Equal(["Check the fridge", "Go to the market"], draft.Select(step => step.Name));
    }

    [Fact]
    public void EmptyingADraftStepKeepsItsName()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.Find(".pspad-step-name input").Change("  ");

        Assert.Equal(["Check the fridge"], draft.Select(step => step.Name));
        Assert.Equal("Check the fridge", steps.Find(".pspad-step-name input").GetAttribute("value"));
    }

    [Fact]
    public void EscapeRevertsADraftStep()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        var field = steps.Find(".pspad-step-name input");
        var typedInto = field.Id;
        field.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(["Check the fridge"], draft.Select(step => step.Name));
        var restored = steps.Find(".pspad-step-name input");
        Assert.NotEqual(typedInto, restored.Id);
        Assert.Equal("Check the fridge", restored.GetAttribute("value"));
    }

    [Fact]
    public async Task PickingTomorrowSetsTheStepDeadline()
    {
        var task = TaskWithStep("Order boxes");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task);
        host.Find(".pspad-step-due").Click();
        host.FindAll(".pspad-due-quick").Single(item => item.TextContent.Contains("Tomorrow")).Click();

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(Today.AddDays(1), Assert.Single(stored!.Steps).DueOn);
    }

    [Fact]
    public async Task ClearDateRemovesTheStepDeadline()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today);
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task);
        host.Find(".pspad-step-due").Click();
        host.Find(".pspad-due-clear").Click();

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(Assert.Single(stored!.Steps).DueOn);
    }

    [Fact]
    public void APastStepDeadlineReadsRed()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today.AddDays(-1));
        AppTestHost.Arrange(this, User, Today, task);

        var due = Host(task).Find(".pspad-step-due");

        Assert.Contains("Yesterday", due.TextContent);
        Assert.Contains("mud-button-text-error", due.ClassName);
    }

    [Fact]
    public void ACheckedStepHasNoDateControl()
    {
        var task = TaskWithStep("Order boxes");
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, task.Steps[0].Id, true), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        Assert.Empty(Host(task).FindAll(".pspad-step-due"));
    }

    [Fact]
    public void ARepeatingTaskHasNoDateControl()
    {
        var task = TaskWithStep("Vacuum");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today)), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        Assert.Empty(Host(task).FindAll(".pspad-step-due"));
    }

    [Fact]
    public void DisabledShowsTheDateAsTextOnly()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today.AddDays(1));
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Pack"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task, disabled: true);

        var due = Assert.Single(host.FindAll(".pspad-step-due"));
        Assert.Contains("Tomorrow", due.TextContent);
        Assert.Empty(due.QuerySelectorAll("button"));
        Assert.NotEqual("BUTTON", due.TagName);
    }

    [Fact]
    public async Task ADraftStepTakesADeadline()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Order boxes") };

        var host = HostDraft(draft, repeating: false);
        host.Find(".pspad-step-due").Click();
        host.FindAll(".pspad-due-quick").Single(item => item.TextContent.Contains("Tomorrow")).Click();

        Assert.Equal(Today.AddDays(1), Assert.Single(draft).DueOn);
        Assert.Equal("Order boxes", draft[0].Name);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void RenamingADraftStepKeepsItsDeadline()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Order boxes", Today.AddDays(1)) };

        var host = HostDraft(draft, repeating: false);
        host.Find(".pspad-step-name input").Change("Order big boxes");

        Assert.Equal(new StepDraft("Order big boxes", Today.AddDays(1)), Assert.Single(draft));
    }

    [Fact]
    public void ARepeatingDraftHasNoDateControl()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Vacuum") };

        Assert.Empty(HostDraft(draft, repeating: true).FindAll(".pspad-step-due"));
    }

    IRenderedComponent<IComponent> Host(TodoTask task, bool disabled = false) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<StepList>(1);
            builder.AddAttribute(2, nameof(StepList.Task), task);
            builder.AddAttribute(3, nameof(StepList.UserId), User);
            builder.AddAttribute(4, nameof(StepList.Today), Today);
            builder.AddAttribute(5, nameof(StepList.Disabled), disabled);
            builder.CloseComponent();
        });

    IRenderedComponent<IComponent> HostDraft(List<StepDraft> draft, bool repeating) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<StepList>(1);
            builder.AddAttribute(2, nameof(StepList.Draft), draft);
            builder.AddAttribute(3, nameof(StepList.UserId), User);
            builder.AddAttribute(4, nameof(StepList.Today), Today);
            builder.AddAttribute(5, nameof(StepList.Repeating), repeating);
            builder.CloseComponent();
        });

    static TodoTask Dated(TodoTask task, DateOnly day)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, task.Steps[0].Id, day), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask TaskWithStep(string name)
    {
        var task = NewTask();
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return task;
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
