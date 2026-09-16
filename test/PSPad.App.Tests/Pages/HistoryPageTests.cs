using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.App.Tests;
using PSPad.Contracts;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class HistoryPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public void EntriesRenderNewestFirstWithTheirDescriptions()
    {
        Arrange(new FakeHistory(
            new HistoryEntry(9, DateTimeOffset.UnixEpoch, "TodoTask", Guid.NewGuid(), "Completed a task"),
            new HistoryEntry(8, DateTimeOffset.UnixEpoch, "Area", Guid.NewGuid(), "Created an area")));

        var page = Render<HistoryPage>();

        var completed = page.Markup.IndexOf("Completed a task", StringComparison.Ordinal);
        var created = page.Markup.IndexOf("Created an area", StringComparison.Ordinal);

        Assert.True(completed >= 0 && completed < created);
    }

    [Fact]
    public void ItRendersTheBurndownChartOnceLoaded()
    {
        Arrange(new FakeHistory(), NewTask("Buy milk", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)));

        var page = Render<HistoryPage>();

        Assert.Equal(2, page.FindComponents<MudChart<double>>().Count);
    }

    [Fact]
    public void ItShowsASkeletonInsteadOfTheChartBeforeTasksHaveLoaded()
    {
        Arrange(new FakeHistory());
        Services.AddSingleton<IDocumentStore<TodoTask>>(new NeverLoadingTaskStore());

        var page = Render<HistoryPage>();

        Assert.Empty(page.FindComponents<MudChart<double>>());
        page.Find(".pspad-burndown-skeleton");
    }

    [Fact]
    public void TheDaysToggleSwitchesTheBurndownWindow()
    {
        Arrange(new FakeHistory(), NewTask("Buy milk", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)));

        var page = Render<HistoryPage>();

        Assert.Equal(30, page.FindComponent<MudChart<double>>().Instance.ChartLabels.Length);

        page.FindAll("button").Single(button => button.TextContent.Contains("90 days")).Click();

        Assert.Equal(90, page.FindComponent<MudChart<double>>().Instance.ChartLabels.Length);
    }

    void Arrange(FakeHistory history, params Aggregate[] documents)
    {
        AppTestHost.Arrange(this, User, Today, documents);
        Services.AddSingleton<IHistorySource>(history);
    }

    static TodoTask NewTask(string name, DateTimeOffset createdAt)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name), createdAt));
        return task;
    }

    sealed class FakeHistory(params HistoryEntry[] entries) : IHistorySource
    {
        public Task<IReadOnlyList<HistoryEntry>> ReadAsync(long? before, int limit) =>
            Task.FromResult<IReadOnlyList<HistoryEntry>>(entries);
    }

    sealed class NeverLoadingTaskStore : IDocumentStore<TodoTask>
    {
        public Task<TodoTask?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<TodoTask?>().Task;

        public Task<IReadOnlyList<TodoTask>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<TodoTask>>().Task;
    }
}
