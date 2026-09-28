using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.State;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Search;
using PSPad.App.Sync;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Dispatch;

[UnitTest]
public class OfflineCascadeTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public async Task DeletingAnAreaOfflineDeletesItsListsAndTasksOnTheDeviceAndQueuesOneCommand()
    {
        var (replica, outbox, sender) = Arrange();
        var (areaId, listId, taskId) = await SeedAsync(sender);
        await outbox.ClearAsync();

        Accepted(await sender.SendAsync(new DeleteArea(Guid.NewGuid(), User, areaId), Ct));

        Assert.True((await replica.LoadAsync<TaskList>(listId))!.Deleted);
        Assert.True((await replica.LoadAsync<TodoTask>(taskId))!.Deleted);
        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public async Task AfterAnOfflineAreaDeleteItsTasksLeaveTodayAndSearch()
    {
        var (replica, _, sender) = Arrange();
        var (areaId, _, _) = await SeedAsync(sender);

        Accepted(await sender.SendAsync(new DeleteArea(Guid.NewGuid(), User, areaId), Ct));

        var counts = new SidebarCounts(
            new ReplicaDocumentStore<TodoTask>(replica),
            new ReplicaDocumentStore<Inbox>(replica),
            new AppState { UserId = User, Today = Today });
        await counts.RefreshAsync();
        var search = new ReplicaSearch(
            new ReplicaDocumentStore<TodoTask>(replica),
            new ReplicaDocumentStore<TaskList>(replica),
            new ReplicaDocumentStore<Area>(replica));
        Assert.Equal(0, counts.Today);
        Assert.Empty(await search.FindAsync(User, "Kup"));
        Assert.Empty(await search.FindAsync(User, "Zakupy"));
    }

    static (InMemoryReplica Replica, InMemoryOutbox Outbox, CommandSender Sender) Arrange()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);
        var services = new ServiceCollection()
            .AddScoped(typeof(IDocumentStore<>), typeof(ReplicaDocumentStore<>))
            .AddSingleton<IReplica>(replica)
            .AddSingleton<IUnitOfWork>(work)
            .AddSingleton<IClock>(new FixedClock(Today))
            .AddPSPadCommands()
            .BuildServiceProvider();

        return (replica, outbox, new CommandSender(services, work, new NoOpSyncTrigger()));
    }

    static async Task<(Guid AreaId, Guid ListId, Guid TaskId)> SeedAsync(CommandSender sender)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        Accepted(await sender.SendAsync(new CreateArea(Guid.NewGuid(), User, areaId, "Dom", 0), Ct));
        Accepted(await sender.SendAsync(new CreateTaskList(Guid.NewGuid(), User, listId, areaId, "Zakupy", 0), Ct));
        Accepted(await sender.SendAsync(new CreateTask(Guid.NewGuid(), User, taskId, listId, "Kup chleb"), Ct));
        Accepted(await sender.SendAsync(new SetTaskDueDate(Guid.NewGuid(), User, taskId, Today), Ct));
        return (areaId, listId, taskId);
    }

    static void Accepted(CommandResult result) => Assert.True(result.Accepted, result.Rejection);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    sealed class NoOpSyncTrigger : ISyncTrigger
    {
        public Task SyncNowAsync() => Task.CompletedTask;
    }
}
