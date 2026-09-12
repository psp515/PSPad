using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class CommandDispatcherTests
{
    [Fact]
    public async Task AnEnvelopeReachesTheRightHandler()
    {
        var work = new FakeUnitOfWork();
        var services = new ServiceCollection()
            .AddSingleton<IUnitOfWork>(work)
            .AddSingleton<IClock>(new FixedClock(DateTimeOffset.UnixEpoch))
            .AddSingleton<IDocumentStore<Area>>(new FakeDocumentStore<Area>())
            .AddPSPadCommands()
            .BuildServiceProvider();
        var user = Guid.NewGuid();
        var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);
        var envelope = new CommandEnvelope(
            nameof(CreateArea), JsonSerializer.SerializeToElement(command));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, user, CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Single(work.Events);
    }

    [Fact]
    public async Task AnEnvelopeForSomebodyElsesUserIdIsRejected()
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
        var envelope = new CommandEnvelope(
            nameof(CreateArea),
            JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0)));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, Guid.NewGuid(), CancellationToken.None);

        Assert.False(response.Accepted);
    }

    [Fact]
    public async Task AnUnknownCommandTypeIsRejectedRatherThanThrown()
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();

        var response = await new CommandDispatcher(services).DispatchAsync(
            new CommandEnvelope("DropDatabase", JsonSerializer.SerializeToElement(new { })),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(response.Accepted);
    }
}
