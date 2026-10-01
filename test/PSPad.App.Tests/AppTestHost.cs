using PSPad.App.Sync;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.Contracts;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Viewport;
using PSPad.App.Statistics;
using PSPad.App.Theme;
using PSPad.App.Updates;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.Tests;

public static class AppTestHost
{
    public static InMemoryReplica Arrange(
        Bunit.TestContext context, Guid userId, DateOnly today, params Aggregate[] documents)
    {
        context.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        context.Services.Options = new ServiceProviderOptions { ValidateScopes = false };
        context.Services.AddMudServices();

        var replica = new InMemoryReplica();
        replica.SetOwnerAsync(userId).GetAwaiter().GetResult();
        foreach (var document in documents)
        {
            replica.SaveAsync(document).GetAwaiter().GetResult();
        }

        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);

        context.Services.AddSingleton<IReplica>(replica);
        context.Services.AddSingleton<IOutbox>(outbox);
        context.Services.AddSingleton(work);
        context.Services.AddSingleton<IUnitOfWork>(work);
        context.Services.AddSingleton<IClock>(new FixedClock(today));
        context.Services.AddSingleton<IDocumentStore<Area>>(new ReplicaDocumentStore<Area>(replica));
        context.Services.AddSingleton<IDocumentStore<TaskList>>(new ReplicaDocumentStore<TaskList>(replica));
        context.Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(replica));
        context.Services.AddSingleton<IDocumentStore<Goal>>(new ReplicaDocumentStore<Goal>(replica));
        context.Services.AddSingleton<IDocumentStore<Inbox>>(new ReplicaDocumentStore<Inbox>(replica));
        context.Services.AddSingleton<IDocumentStore<ReferenceItem>>(new ReplicaDocumentStore<ReferenceItem>(replica));
        context.Services.AddSingleton<IDocumentStore<AreaView>>(new ReplicaDocumentStore<AreaView>(replica));
        context.Services.AddSingleton<IDocumentStore<ListView>>(new ReplicaDocumentStore<ListView>(replica));
        context.Services.AddPSPadCommands();
        context.Services.AddSingleton(new AppState { UserId = userId, Today = today });
        context.Services.AddSingleton(new PageHeader());
        context.Services.AddSingleton(new ThemePreference(context.JSInterop.JSRuntime));

        var collapse = new CardCollapseState(context.JSInterop.JSRuntime);
        collapse.LoadAsync().GetAwaiter().GetResult();
        context.Services.AddSingleton(collapse);

        context.Services.AddSingleton(new LastArea(context.JSInterop.JSRuntime));

        var statisticsCache = new StatisticsCache(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(statisticsCache);

        var syncTrigger = new NoOpSyncTrigger();
        context.Services.AddSingleton(services => new CommandSender(services, work, syncTrigger));
        context.Services.AddSingleton<ISyncTrigger>(syncTrigger);
        context.Services.AddSingleton(new ReplicaOwnership(replica, outbox, statisticsCache));
        context.Services.AddScoped<Clipboard>();
        context.Services.AddSingleton<IViewport>(new FakeViewport(isDesktop: true));
        context.Services.AddSingleton<IBreakpoints>(new FakeBreakpoints(MudBlazor.Breakpoint.Xs));
        context.Services.AddSingleton<ServerReachability>();
        context.Services.AddSingleton<IConnectivity>(new AlwaysOnline());
        context.Services.AddSingleton<IAppUpdates>(new FakeAppUpdates());
        context.Services.AddSingleton<ISnapshotsApi>(new NoOpSnapshotsApi());

        return replica;
    }

    sealed class AlwaysOnline : IConnectivity
    {
        public bool IsOnline => true;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    public sealed class FakeAppUpdates : IAppUpdates
    {
        public bool IsAvailable { get; private set; }

        public int Applied { get; private set; }

        public event Action? Available;

        public void Announce()
        {
            IsAvailable = true;
            Available?.Invoke();
        }

        public Task ApplyAsync()
        {
            Applied++;
            return Task.CompletedTask;
        }
    }

    sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public sealed class FakeViewport(bool isDesktop) : IViewport
    {
        readonly List<Action<bool>> _subscribers = [];

        public Task SubscribeAsync(Action<bool> onDesktopChanged)
        {
            _subscribers.Add(onDesktopChanged);
            onDesktopChanged(isDesktop);
            return Task.CompletedTask;
        }

        public void ChangeTo(bool desktop)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber(desktop);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public sealed class FakeBreakpoints(MudBlazor.Breakpoint breakpoint) : IBreakpoints
    {
        readonly List<Action<MudBlazor.Breakpoint>> _subscribers = [];

        public Task SubscribeAsync(Action<MudBlazor.Breakpoint> onChanged)
        {
            _subscribers.Add(onChanged);
            onChanged(breakpoint);
            return Task.CompletedTask;
        }

        public void ChangeTo(MudBlazor.Breakpoint changed)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber(changed);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class NoOpSyncTrigger : ISyncTrigger
    {
        public Task SyncNowAsync() => Task.CompletedTask;
    }

    sealed class NoOpSnapshotsApi : ISnapshotsApi
    {
        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt) =>
            Task.FromResult<PublishedSnapshotView?>(null);

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId) =>
            Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([]);

        public Task<bool> RevokeAsync(Guid snapshotId) => Task.FromResult(false);

        public Task<bool> RecordVisitAsync(string token) => Task.FromResult(false);

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Task.FromResult<IReadOnlyList<SnapshotVisitView>>([]);
    }
}
