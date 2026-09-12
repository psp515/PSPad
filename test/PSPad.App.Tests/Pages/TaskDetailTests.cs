using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class TaskDetailTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void StepsRenderInPositionOrder()
    {
        var task = WithSteps("first", "second");
        var page = Render(task);

        var firstIndex = page.Markup.IndexOf("first", StringComparison.Ordinal);
        var secondIndex = page.Markup.IndexOf("second", StringComparison.Ordinal);

        Assert.True(firstIndex < secondIndex);
    }

    [Fact]
    public void ARecurringTaskShowsAMissedDayAsSkippedRatherThanOverdue()
    {
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today.AddDays(-3))),
            DateTimeOffset.UnixEpoch));

        var page = Render(task);

        Assert.Contains("Skipped", page.Markup);
        Assert.DoesNotContain("Overdue", page.Markup);
    }

    IRenderedComponent<TaskDetail> Render(TodoTask task)
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        var replica = new InMemoryReplica();
        replica.SaveAsync(task).GetAwaiter().GetResult();
        Services.AddSingleton<IReplica>(replica);
        Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(replica));
        Services.AddSingleton(new AppState { UserId = User, TimeZone = "Etc/UTC", Today = Today });

        return Render<TaskDetail>(parameters => parameters.Add(page => page.TaskId, task.Id));
    }

    static TodoTask WithSteps(params string[] names)
    {
        var task = NewTask("Buy milk");
        foreach (var name in names)
        {
            task.ApplyAll(TodoTask.Decide(
                task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), name),
                DateTimeOffset.UnixEpoch));
        }

        return task;
    }

    static TodoTask NewTask(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
