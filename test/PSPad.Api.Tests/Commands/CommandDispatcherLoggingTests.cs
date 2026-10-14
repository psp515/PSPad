using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PSPad.Api.Commands;
using PSPad.Api.Security;
using PSPad.Api.Tests.Logging;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class CommandDispatcherLoggingTests
{
    readonly RecordingLogger<CommandDispatcher> _logger = new();
    readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task AnUnknownCommandIsLoggedAsAWarning()
    {
        await DispatchAsync(new CommandEnvelope("DropDatabase", JsonSerializer.SerializeToElement(new { })));

        AssertRefused("DropDatabase", "unknown command");
    }

    [Fact]
    public async Task AMalformedPayloadIsLoggedWithoutThePayload()
    {
        var payload = JsonSerializer.SerializeToElement(new { areaId = "secret-looking-value" });

        await DispatchAsync(new CommandEnvelope(nameof(CreateArea), payload));

        var entry = AssertRefused(nameof(CreateArea), "malformed payload");
        Assert.DoesNotContain("secret-looking-value", entry.Message);
    }

    [Fact]
    public async Task ACommandForAnotherUserIsLoggedWithTheCallersId()
    {
        var command = new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0);

        await DispatchAsync(new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command)));

        AssertRefused(nameof(CreateArea), "different user");
    }

    [Fact]
    public async Task AServerOnlyCommandFromAClientIsLogged()
    {
        var join = new JoinTaskList(Guid.NewGuid(), _user, Guid.NewGuid(), "token-value", "code-value", "Anna");

        await DispatchAsync(new CommandEnvelope(nameof(JoinTaskList), JsonSerializer.SerializeToElement(join)));

        var entry = AssertRefused(nameof(JoinTaskList), "server-only command");
        Assert.DoesNotContain("token-value", entry.Message);
        Assert.DoesNotContain("code-value", entry.Message);
    }

    [Fact]
    public async Task AnOverlongCommandTypeIsCutShortInTheLog()
    {
        var type = new string('x', 10_000);

        await DispatchAsync(new CommandEnvelope(type, JsonSerializer.SerializeToElement(new { })));

        var entry = Assert.Single(_logger.Logs.Entries);
        Assert.True(entry.Message.Length < 500);
    }

    async Task DispatchAsync(CommandEnvelope envelope)
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
        await new CommandDispatcher(services, _logger).DispatchAsync(envelope, _user, CancellationToken.None);
    }

    LogEntry AssertRefused(string commandType, string reason)
    {
        var entry = Assert.Single(_logger.Logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(SecurityLog.CommandRefusedId, entry.Id.Id);
        Assert.Contains(_user.ToString(), entry.Message);
        Assert.Contains(commandType, entry.Message);
        Assert.Contains(reason, entry.Message);
        return entry;
    }
}
