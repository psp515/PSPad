using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class TodayTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ATaskDueYesterdayRendersAsOverdue()
    {
        var today = new DateOnly(2026, 9, 12);
        Arrange(today, DueTask("Buy milk", today.AddDays(-1)));

        var page = Render<Today>();

        Assert.Contains("Buy milk", page.Markup);
        Assert.Contains("overdue", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARecurringTaskMissedYesterdayIsNotRenderedAsOverdue()
    {
        var today = new DateOnly(2026, 9, 12);
        Arrange(today, RecurringTask("Read a book", today.AddDays(-7)));

        var page = Render<Today>();

        Assert.Contains("Read a book", page.Markup);
        Assert.DoesNotContain("overdue", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATaskDueTomorrowIsNotOnTheScreen()
    {
        var today = new DateOnly(2026, 9, 12);
        Arrange(today, DueTask("Later", today.AddDays(1)));

        var page = Render<Today>();

        Assert.DoesNotContain("Later", page.Markup);
    }

    void Arrange(DateOnly today, params TodoTask[] tasks)
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        var replica = new InMemoryReplica();
        foreach (var task in tasks)
        {
            replica.SaveAsync(task).GetAwaiter().GetResult();
        }

        Services.AddSingleton<IReplica>(replica);
        Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(replica));
        Services.AddSingleton<IDocumentStore<Inbox>>(new ReplicaDocumentStore<Inbox>(replica));
        Services.AddSingleton(new FabContext());
        Services.AddSingleton(new AppState { UserId = User, TimeZone = "Etc/UTC", Today = today });
    }

    static TodoTask DueTask(string name, DateOnly due)
    {
        var task = NewTask(name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask RecurringTask(string name, DateOnly from)
    {
        var task = NewTask(name);
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(
                Guid.NewGuid(), User, task.Id, Module.Tasks.Recurrence.RecurrenceRule.Daily(from)),
            DateTimeOffset.UnixEpoch));
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
