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
        var field = steps.Find("input[aria-label='Add step']");
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

        Assert.Equal($"{ContentLimits.Name}", steps.Find("input[aria-label='Add step']").GetAttribute("maxlength"));
    }

    [Fact]
    public async Task ABlankStepIsNotAdded()
    {
        var task = NewTask();
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var field = steps.Find("input[aria-label='Add step']");
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
        steps.Find("button[aria-label='Remove Check the fridge']").Click();

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

        Assert.Contains("Marked on a snapshot", steps.Text());
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

        Assert.DoesNotContain("Marked on a snapshot", steps.Text());
    }

    [Fact]
    public async Task ADraftCollectsStepsInOrderWithoutSendingCommands()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft>();

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        AddField(steps).Input("Check the fridge");
        AddField(steps).KeyDown(new KeyboardEventArgs { Key = "Enter" });
        AddField(steps).Input("   ");
        AddField(steps).KeyDown(new KeyboardEventArgs { Key = "Enter" });
        AddField(steps).Input("Go to the shop");
        AddField(steps).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Check the fridge", "Go to the shop"], draft.Select(step => step.Name));
        Assert.Equal(["Check the fridge", "Go to the shop"],
            steps.FindAll("input[aria-label^='Rename ']").Select(name => name.GetAttribute("value")));
        Assert.Empty(steps.FindAll("input[type=checkbox]"));
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void RemovingADraftStepDropsItFromTheDraft()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge"), new("Go to the shop") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.Find("button[aria-label='Remove Check the fridge']").Click();

        Assert.Equal(["Go to the shop"], draft.Select(step => step.Name));
        Assert.Single(steps.FindAll("input[aria-label^='Rename ']"));
    }

    [Fact]
    public async Task ChangingAStepNameRenamesIt()
    {
        var task = TaskWithStep("Check the fridge");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find("input[aria-label^='Rename ']").Change("  Check the freezer ");

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal("Check the freezer", stored!.Steps.Single().Name);
    }

    [Fact]
    public async Task AnUnchangedStepNameChangesNothing()
    {
        var task = TaskWithStep("Check the fridge");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find("input[aria-label^='Rename ']").Change(" Check the fridge ");

        Assert.Equal(task.Version, (await replica.LoadAsync<TodoTask>(task.Id))!.Version);
    }

    [Fact]
    public async Task AnEmptiedStepNameRevertsAndChangesNothing()
    {
        var task = TaskWithStep("Check the fridge");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        steps.Find("input[aria-label^='Rename ']").Change("   ");

        Assert.Equal("Check the fridge", Assert.Single((await replica.LoadAsync<TodoTask>(task.Id))!.Steps).Name);
        Assert.Equal("Check the fridge", steps.Find("input[aria-label^='Rename ']").GetAttribute("value"));
    }

    [Fact]
    public void TheStepNameFieldIsLabelledAndCapped()
    {
        var task = TaskWithStep("Check the fridge");
        AppTestHost.Arrange(this, User, Today, task);

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Task, task).Add(p => p.UserId, User));
        var input = steps.Find("input[aria-label^='Rename ']");

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

        Assert.True(steps.Find("input[aria-label^='Rename ']").HasAttribute("disabled"));
    }

    [Fact]
    public void EditingADraftStepRewritesItInPlace()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge"), new("Go to the shop") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.FindAll("input[aria-label^='Rename ']")[1].Change(" Go to the market ");

        Assert.Equal(["Check the fridge", "Go to the market"], draft.Select(step => step.Name));
    }

    [Fact]
    public void EmptyingADraftStepKeepsItsName()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Check the fridge") };

        var steps = Render<StepList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));
        steps.Find("input[aria-label^='Rename ']").Change("  ");

        Assert.Equal(["Check the fridge"], draft.Select(step => step.Name));
        Assert.Equal("Check the fridge", steps.Find("input[aria-label^='Rename ']").GetAttribute("value"));
    }

    [Fact]
    public async Task PickingTomorrowSetsTheStepDeadline()
    {
        var task = TaskWithStep("Order boxes");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task);
        host.Find("button[aria-label='Set a deadline for Order boxes']").Click();
        Item(host, "Tomorrow").Click();

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(Today.AddDays(1), Assert.Single(stored!.Steps).DueOn);
    }

    [Fact]
    public async Task ClearDateRemovesTheStepDeadline()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today);
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task);
        DueButton(host, "Today").Click();
        Item(host, "Clear date").Click();

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(Assert.Single(stored!.Steps).DueOn);
    }

    [Fact]
    public void APastStepDeadlineReadsRelatively()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today.AddDays(-1));
        AppTestHost.Arrange(this, User, Today, task);

        Assert.NotNull(DueButton(Host(task), "Yesterday"));
    }

    [Fact]
    public void ACheckedStepHasNoDateControl()
    {
        var task = TaskWithStep("Order boxes");
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, task.Steps[0].Id, true), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        Assert.Empty(Host(task).FindAll("button[aria-label='Set a deadline for Order boxes']"));
    }

    [Fact]
    public void ARepeatingTaskHasNoDateControl()
    {
        var task = TaskWithStep("Vacuum");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today)), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        Assert.Empty(Host(task).FindAll("button[aria-label='Set a deadline for Vacuum']"));
    }

    [Fact]
    public void DisabledShowsTheDateAsTextOnly()
    {
        var task = Dated(TaskWithStep("Order boxes"), Today.AddDays(1));
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Pack"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var host = Host(task, disabled: true);

        Assert.Contains("Tomorrow", host.Text());
        Assert.DoesNotContain(host.FindAll("button"), button => button.TextContent.Contains("Tomorrow"));
        Assert.DoesNotContain(host.FindAll("button"), button => button.GetAttribute("aria-label")?.StartsWith("Set a deadline") == true);
    }

    [Fact]
    public async Task ADraftStepTakesADeadline()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Order boxes") };

        var host = HostDraft(draft, repeating: false);
        host.Find("button[aria-label='Set a deadline for Order boxes']").Click();
        Item(host, "Tomorrow").Click();

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
        host.Find("input[aria-label^='Rename ']").Change("Order big boxes");

        Assert.Equal(new StepDraft("Order big boxes", Today.AddDays(1)), Assert.Single(draft));
    }

    [Fact]
    public void ARepeatingDraftHasNoDateControl()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<StepDraft> { new("Vacuum") };

        Assert.Empty(HostDraft(draft, repeating: true).FindAll("button[aria-label='Set a deadline for Vacuum']"));
    }

    static AngleSharp.Dom.IElement AddField(IRenderedComponent<StepList> steps) => steps.Find("input[aria-label='Add step'], input[aria-label='Next step']");

    static AngleSharp.Dom.IElement Item(IRenderedComponent<IComponent> host, string text) =>
        host.FindAll("[role=menuitem]").Single(item => item.TextContent.Contains(text));

    static AngleSharp.Dom.IElement DueButton(IRenderedComponent<IComponent> host, string text) =>
        host.FindAll("button").Single(button => button.TextContent.Contains(text));

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
