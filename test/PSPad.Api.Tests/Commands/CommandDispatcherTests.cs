using PSPad.Api.Tests.Sync;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
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
        Assert.True(response.Unrecoverable);
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
        Assert.True(response.Unrecoverable);
    }

    [Fact]
    public async Task AMalformedPayloadIsRejectedAsUnrecoverable()
    {
        var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
        var envelope = new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement<object?>(null));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, Guid.NewGuid(), CancellationToken.None);

        Assert.False(response.Accepted);
        Assert.True(response.Unrecoverable);
    }

    [Fact]
    public async Task ACommandWithNoRegisteredHandlerIsRejectedAsUnrecoverable()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var user = Guid.NewGuid();
        var envelope = new CommandEnvelope(
            nameof(CreateArea),
            JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, user, CancellationToken.None);

        Assert.False(response.Accepted);
        Assert.True(response.Unrecoverable);
    }

    [Fact]
    public async Task AnAcceptedCommandIsNotMarkedUnrecoverable()
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
        var envelope = new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command));

        var response = await new CommandDispatcher(services).DispatchAsync(
            envelope, user, CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.False(response.Unrecoverable);
    }
}

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandDispatcherServerOnlyTests(MongoFixture fixture)
{
    [Fact]
    public async Task AServerOnlyCommandIsRefusedFromAClient()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var join = new JoinTaskList(Guid.NewGuid(), user, Guid.NewGuid(), "k3Jv9s2mQ0x7b1nR4tYw8eZa", Sharing.Code, "Anna");

        var response = await client.PostAsJsonAsync("/api/commands",
            new[] { new CommandEnvelope(nameof(JoinTaskList), JsonSerializer.SerializeToElement(join)) }, ct);
        var result = Assert.Single((await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!);

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
        Assert.Equal("JoinTaskList cannot be sent from a client.", result.Rejection);
    }

    [Fact]
    public async Task TheServerRunsAServerOnlyCommandItself()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = (await owner.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");

        await Send(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Zakupy"),
            new ShareTaskList(Guid.NewGuid(), ownerId, listId, token, Sharing.Code, "Owner"));

        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = (await member.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CommandDispatcher>()
            .RunAsync(new JoinTaskList(Guid.NewGuid(), memberId, listId, token, Sharing.Code, "Anna"), ct);

        Assert.True(result.Accepted, result.Rejection);

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var row = Assert.Single(sync!.Documents["tasklists"]);
        Assert.Contains(
            row.GetProperty("_members").EnumerateArray(),
            member => member.GetProperty("userId").GetGuid() == memberId);
    }

    static async Task Send(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }
}
