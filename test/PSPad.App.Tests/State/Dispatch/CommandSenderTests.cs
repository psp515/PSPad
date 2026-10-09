using PSPad.Abstractions;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Dispatch;

[UnitTest]
public class CommandSenderTests
{
    [Fact]
    public async Task SentFiresAndSyncIsTriggeredWhenAHandlerRuns()
    {
        var handled = CommandResult.Ok();
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(handled)), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(1, fires);
        Assert.Same(handled, result);
        Assert.Equal(1, sync.Calls);
    }

    [Fact]
    public async Task SentStaysSilentAndSyncIsNotTriggeredWhenTheHandlerRejects()
    {
        var rejected = CommandResult.Rejected("nope");
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(rejected)), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task SentStaysSilentAndSyncIsNotTriggeredWhenNoHandlerIsRegistered()
    {
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(new FakeServiceProvider(handler: null), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task CommandsSentAtTheSameTimeCommitOneAfterTheOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var replica = new PausingReplica();
        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);
        var sender = new CommandSender(new StagingServiceProvider(work), work, new FakeSyncTrigger());
        var first = new StagingCommand(Guid.NewGuid(), Guid.NewGuid());
        var second = new StagingCommand(Guid.NewGuid(), Guid.NewGuid());

        var sendingFirst = sender.SendAsync(first, ct);
        await replica.Paused.Task;
        var sendingSecond = sender.SendAsync(second, ct);
        replica.Resume();
        await Task.WhenAll(sendingFirst, sendingSecond);

        var entries = await outbox.PeekAsync(10);
        Assert.Equal([first.CommandId, second.CommandId], entries.Select(entry => entry.CommandId));
        Assert.Equal(2, replica.Saves);
    }

    [Fact]
    public async Task ARejectedCommandNeverReachesTheOutboxWithTheNextOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(new InMemoryReplica(), outbox);
        var rejecting = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(CommandResult.Rejected("nope"))), work, new FakeSyncTrigger());
        var accepting = new CommandSender(new StagingServiceProvider(work), work, new FakeSyncTrigger());
        var accepted = new StagingCommand(Guid.NewGuid(), Guid.NewGuid());

        await rejecting.SendAsync(new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), ct);
        await accepting.SendAsync(accepted, ct);

        var entry = Assert.Single(await outbox.PeekAsync(10));
        Assert.Equal(nameof(StagingCommand), entry.Envelope.Type);
    }

    static ReplicaUnitOfWork Work() => new(new InMemoryReplica(), new InMemoryOutbox());

    sealed record FakeCommand(Guid CommandId, Guid UserId) : ICommand;

    sealed class FakeCommandHandler(CommandResult result) : ICommandHandler<FakeCommand>
    {
        public Task<CommandResult> HandleAsync(FakeCommand command, CancellationToken ct) =>
            Task.FromResult(result);
    }

    sealed class FakeServiceProvider(object? handler) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ICommandHandler<FakeCommand>) ? handler : null;
    }

    sealed record StagingCommand(Guid CommandId, Guid UserId) : ICommand;

    sealed class StagedThing : Aggregate
    {
        public StagedThing() => Id = Guid.NewGuid();

        protected override void When(DomainEvent @event)
        {
        }
    }

    sealed class StagingHandler(ReplicaUnitOfWork work) : ICommandHandler<StagingCommand>
    {
        public async Task<CommandResult> HandleAsync(StagingCommand command, CancellationToken ct)
        {
            work.Stage(new StagedThing(), []);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
    }

    sealed class StagingServiceProvider(ReplicaUnitOfWork work) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ICommandHandler<StagingCommand>) ? new StagingHandler(work) : null;
    }

    sealed class PausingReplica : IReplica
    {
        readonly InMemoryReplica _inner = new();
        readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Saves { get; private set; }

        public void Resume() => _resume.TrySetResult();

        public async Task SaveAsync(Aggregate aggregate)
        {
            Saves++;
            if (Saves == 1)
            {
                Paused.TrySetResult();
                await _resume.Task;
            }

            await _inner.SaveAsync(aggregate);
        }

        public Task<T?> LoadAsync<T>(Guid id) where T : Aggregate => _inner.LoadAsync<T>(id);

        public Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate => _inner.LoadAllAsync<T>(userId);

        public Task RemoveAsync(Guid id) => _inner.RemoveAsync(id);

        public Task<long> MarkerAsync() => _inner.MarkerAsync();

        public Task SetMarkerAsync(long marker) => _inner.SetMarkerAsync(marker);

        public Task<string?> CollectionsFingerprintAsync() => _inner.CollectionsFingerprintAsync();

        public Task SetCollectionsFingerprintAsync(string fingerprint) => _inner.SetCollectionsFingerprintAsync(fingerprint);

        public Task<DateTimeOffset?> LastSyncedAtAsync() => _inner.LastSyncedAtAsync();

        public Task SetLastSyncedAtAsync(DateTimeOffset at) => _inner.SetLastSyncedAtAsync(at);

        public Task<Guid?> OwnerAsync() => _inner.OwnerAsync();

        public Task SetOwnerAsync(Guid userId) => _inner.SetOwnerAsync(userId);

        public Task ClearAsync() => _inner.ClearAsync();
    }

    sealed class FakeSyncTrigger : ISyncTrigger
    {
        public int Calls { get; private set; }

        public Task SyncNowAsync()
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
