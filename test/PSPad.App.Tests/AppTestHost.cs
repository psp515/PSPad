using System.Text.Json;
using PSPad.App.Sync;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Localization;
using PSPad.App.State;
using PSPad.Contracts;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.State.Viewport;
using PSPad.App.Statistics;
using PSPad.App.Theme;
using PSPad.App.Updates;
using PSPad.Module.Presentation;
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
        context.Services.AddLocalization();
        context.Services.AddSingleton(new LanguagePreference(context.JSInterop.JSRuntime));

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
        context.Services.AddScoped<IViewTargets, StoreViewTargets>();
        context.Services.AddSingleton<IDocumentStore<Area>>(new ReplicaDocumentStore<Area>(replica));
        context.Services.AddSingleton<IDocumentStore<TaskList>>(new ReplicaDocumentStore<TaskList>(replica));
        context.Services.AddSingleton<IDocumentStore<TodoTask>>(new ReplicaDocumentStore<TodoTask>(replica));
        context.Services.AddSingleton<IDocumentStore<Goal>>(new ReplicaDocumentStore<Goal>(replica));
        context.Services.AddSingleton<IDocumentStore<Inbox>>(new ReplicaDocumentStore<Inbox>(replica));
        context.Services.AddSingleton<IDocumentStore<Module.Money.Budgets.Budget>>(new ReplicaDocumentStore<Module.Money.Budgets.Budget>(replica));
        context.Services.AddSingleton<IDocumentStore<Module.Money.Preferences.MoneyPreferences>>(new ReplicaDocumentStore<Module.Money.Preferences.MoneyPreferences>(replica));
        context.Services.AddSingleton<IDocumentStore<Module.Money.Entries.MoneyEntry>>(new ReplicaDocumentStore<Module.Money.Entries.MoneyEntry>(replica));
        context.Services.AddSingleton<IDocumentStore<Module.Money.Balance.BalanceSnapshot>>(new ReplicaDocumentStore<Module.Money.Balance.BalanceSnapshot>(replica));
        context.Services.AddSingleton<IDocumentStore<ReferenceItem>>(new ReplicaDocumentStore<ReferenceItem>(replica));
        context.Services.AddSingleton<IDocumentStore<AreaView>>(new ReplicaDocumentStore<AreaView>(replica));
        context.Services.AddSingleton<IDocumentStore<ListView>>(new ReplicaDocumentStore<ListView>(replica));
        context.Services.AddPSPadCommands();
        context.Services.AddSingleton(new AppState { UserId = userId, Today = today });
        context.Services.AddSingleton(new PageHeader());
        context.Services.AddSingleton(new StatusBelts());
        context.Services.AddSingleton(new ThemePreference(context.JSInterop.JSRuntime));

        var collapse = new CardCollapseState(context.JSInterop.JSRuntime);
        collapse.LoadAsync().GetAwaiter().GetResult();
        context.Services.AddSingleton(collapse);
        context.Services.AddSingleton(new ListScreenState(context.JSInterop.JSRuntime));
        context.Services.AddSingleton(new NavGroupState(context.JSInterop.JSRuntime));

        context.Services.AddSingleton(new LastArea(context.JSInterop.JSRuntime));

        var statisticsCache = new StatisticsCache(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(statisticsCache);

        var syncTrigger = new NoOpSyncTrigger();
        context.Services.AddSingleton(services => new CommandSender(services, work, syncTrigger));
        context.Services.AddSingleton<ISyncTrigger>(syncTrigger);
        context.Services.AddSingleton<ISyncStatus>(syncTrigger);
        context.Services.AddSingleton<ISnapshotCache>(new InMemorySnapshotCache());
        context.Services.AddSingleton(services =>
            new ReplicaOwnership(replica, outbox, statisticsCache, services.GetRequiredService<ISnapshotCache>()));
        context.Services.AddScoped<Clipboard>();
        context.Services.AddSingleton<IViewport>(new FakeViewport(isDesktop: true));
        context.Services.AddSingleton<IBreakpoints>(new FakeBreakpoints(MudBlazor.Breakpoint.Xs));
        context.Services.AddSingleton<ServerReachability>();
        context.Services.AddSingleton<IConnectivity>(new AlwaysOnline());
        context.Services.AddSingleton<ISnapshotsApi>(new NoOpSnapshotsApi());
        context.Services.AddSingleton<INbpRates>(new FakeNbpRates());

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

    public sealed class FakeNbpRates : INbpRates
    {
        public NbpRateView? Next { get; set; }

        public Dictionary<string, NbpRateView?> ByCurrency { get; } = [];

        public TaskCompletionSource<NbpRateView?>? Gate { get; set; }

        public Dictionary<string, TaskCompletionSource<NbpRateView?>> GatesByCurrency { get; } = [];

        public List<(string Currency, DateOnly Date)> Asked { get; } = [];

        public Task<NbpRateView?> RateAsync(string currency, DateOnly date)
        {
            Asked.Add((currency, date));
            return GatesByCurrency.TryGetValue(currency, out var gate) ? gate.Task : Gate?.Task ?? Task.FromResult(ByCurrency.TryGetValue(currency, out var rate) ? rate : Next);
        }
    }

    sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    sealed class FakeViewport(bool isDesktop) : IViewport
    {
        public Task SubscribeAsync(Action<bool> onDesktopChanged)
        {
            onDesktopChanged(isDesktop);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class FakeBreakpoints(MudBlazor.Breakpoint breakpoint) : IBreakpoints
    {
        public Task SubscribeAsync(Action<MudBlazor.Breakpoint> onChanged)
        {
            onChanged(breakpoint);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class NoOpSyncTrigger : ISyncTrigger, ISyncStatus
    {
        public bool IsSyncing => false;

        public bool LastSyncFailed => false;

        public DateTimeOffset? LastSyncedAt => null;

        public IReadOnlyList<string> LastRejections => [];

        public bool HasStuckChange => false;

        public Task DiscardStuckChangeAsync() => Task.CompletedTask;

#pragma warning disable CS0067
        public event Action? Changed;
#pragma warning restore CS0067

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
