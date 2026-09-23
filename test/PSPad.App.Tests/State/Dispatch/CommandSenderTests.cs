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
