using PSPad.Abstractions;
using PSPad.App.State;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Dispatch;

[UnitTest]
public class CommandSenderTests
{
    [Fact]
    public async Task SentFiresExactlyOnceWhenAHandlerRuns()
    {
        var handled = CommandResult.Ok();
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(handled)), Work());
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(1, fires);
        Assert.Same(handled, result);
    }

    [Fact]
    public async Task SentStaysSilentWhenTheHandlerRejects()
    {
        var rejected = CommandResult.Rejected("nope");
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(rejected)), Work());
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task SentStaysSilentWhenNoHandlerIsRegistered()
    {
        var sender = new CommandSender(new FakeServiceProvider(handler: null), Work());
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
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
}
