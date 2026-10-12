using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Infrastructure.Mongo;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class ConflictRetryTests
{
    public sealed record Touch(Guid CommandId, Guid UserId) : ICommand;

    public sealed class ConflictingHandler(int conflicts) : ICommandHandler<Touch>
    {
        public int Runs { get; private set; }

        public async Task<CommandResult> HandleAsync(Touch command, CancellationToken ct)
        {
            await Task.Yield();
            Runs++;
            return Runs <= conflicts ? throw new ConcurrencyConflictException(command.CommandId) : CommandResult.Ok();
        }
    }

    static (CommandDispatcher Dispatcher, ConflictingHandler Handler) With(int conflicts)
    {
        var handler = new ConflictingHandler(conflicts);
        var services = new ServiceCollection().AddSingleton<ICommandHandler<Touch>>(handler).BuildServiceProvider();
        return (new CommandDispatcher(services), handler);
    }

    [Fact]
    public async Task ConflictsAreRetriedOnFreshDataUntilTheCommandLands()
    {
        var (dispatcher, handler) = With(conflicts: CommandDispatcher.MostAttempts - 1);

        var result = await dispatcher.RunAsync(new Touch(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(CommandDispatcher.MostAttempts, handler.Runs);
    }

    [Fact]
    public async Task ConflictsPastTheLastAttemptAreRejectedNotRetriedForever()
    {
        var (dispatcher, handler) = With(conflicts: CommandDispatcher.MostAttempts);

        var result = await dispatcher.RunAsync(new Touch(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("Someone else changed this at the same time. Try again.", result.Rejection);
        Assert.Equal(CommandDispatcher.MostAttempts, handler.Runs);
    }
}
