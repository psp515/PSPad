using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PSPad.Api.Security;
using PSPad.Api.Tests.Logging;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandRefusalLoggingTests(MongoFixture fixture)
{
    ApiFactory Factory() => new(fixture, testServices: services => services.AddRecordedLogs());

    [Fact]
    public async Task AnUnknownCommandIsLoggedAsAWarning()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);

        await PostAsync(client, new CommandEnvelope("DropDatabase", JsonSerializer.SerializeToElement(new { })), ct);

        AssertRefused(factory, user, "DropDatabase", "unknown command");
    }

    [Fact]
    public async Task AMalformedPayloadIsLoggedWithoutThePayload()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var payload = JsonSerializer.SerializeToElement(new { areaId = "secret-looking-value" });

        await PostAsync(client, new CommandEnvelope(nameof(CreateArea), payload), ct);

        var entry = AssertRefused(factory, user, nameof(CreateArea), "malformed payload");
        Assert.DoesNotContain("secret-looking-value", entry.Message);
    }

    [Fact]
    public async Task ACommandForAnotherUserIsLoggedWithTheCallersId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var command = new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0);

        await PostAsync(client, new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command)), ct);

        AssertRefused(factory, user, nameof(CreateArea), "different user");
    }

    [Fact]
    public async Task AServerOnlyCommandFromAClientIsLogged()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var join = new JoinTaskList(Guid.NewGuid(), user, Guid.NewGuid(), "token-value", "code-value", "Anna");

        await PostAsync(client, new CommandEnvelope(nameof(JoinTaskList), JsonSerializer.SerializeToElement(join)), ct);

        var entry = AssertRefused(factory, user, nameof(JoinTaskList), "server-only command");
        Assert.DoesNotContain("token-value", entry.Message);
        Assert.DoesNotContain("code-value", entry.Message);
    }

    [Fact]
    public async Task AnOverlongCommandTypeIsCutShortInTheLog()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(client, ct);

        await PostAsync(client, new CommandEnvelope(new string('x', 10_000), JsonSerializer.SerializeToElement(new { })), ct);

        var entry = Assert.Single(Refusals(factory));
        Assert.True(entry.Message.Length < 500);
    }

    static async Task PostAsync(HttpClient client, CommandEnvelope envelope, CancellationToken ct) =>
        (await client.PostAsJsonAsync("/api/commands", new[] { envelope }, ct)).EnsureSuccessStatusCode();

    static IEnumerable<LogEntry> Refusals(ApiFactory factory) =>
        factory.Services.GetRequiredService<RecordedLogs>().Entries
            .Where(entry => entry.Id.Id == SecurityLog.CommandRefusedId);

    static LogEntry AssertRefused(ApiFactory factory, Guid user, string commandType, string reason)
    {
        var entry = Assert.Single(Refusals(factory));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(user.ToString(), entry.Message);
        Assert.Contains(commandType, entry.Message);
        Assert.Contains(reason, entry.Message);
        return entry;
    }
}
