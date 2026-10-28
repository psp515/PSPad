using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnAcceptedCommandComesBackAcceptedAndIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command))
        }, ct);

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        var result = Assert.Single(results!);
        Assert.True(result.Accepted);
        Assert.False(result.Unrecoverable);
    }

    [Fact]
    public async Task ABatchIsAppliedInOrderAndReportsEachResult()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var areaId = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new RenameArea(Guid.NewGuid(), user, areaId, "House")),
            Envelope(new RenameArea(Guid.NewGuid(), user, Guid.NewGuid(), "Nowhere"))
        }, ct);

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);

        Assert.True(results![0].Accepted);
        Assert.True(results[1].Accepted);
        Assert.False(results[2].Accepted);
        Assert.NotNull(results[2].Rejection);
    }

    [Fact]
    public async Task ReplayingTheSameBatchChangesNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var baseline = await client.GetFromJsonAsync<JsonElement>("/api/sync?since=0", ct);
        var marker = baseline.GetProperty("marker").GetInt64();
        var batch = new[] { Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)) };

        await client.PostAsJsonAsync("/api/commands", batch, ct);
        var replay = await client.PostAsJsonAsync("/api/commands", batch, ct);

        var results = await replay.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.True(Assert.Single(results!).Accepted);

        var events = await Persistence.TestContext.For(fixture).Collection<StoredEvent>("events")
            .CountDocumentsAsync(
                Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user) &
                Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, marker),
                cancellationToken: ct);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task ABatchAtTheCapIsAccepted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var batch = Enumerable.Range(0, CommandBatch.MaxEnvelopes)
            .Select(position => Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), $"Area {position}", position)))
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/commands", batch, ct);

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.Equal(CommandBatch.MaxEnvelopes, results!.Length);
    }

    [Fact]
    public async Task ABatchOverTheCapIsRefusedAndNothingIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var batch = Enumerable.Range(0, CommandBatch.MaxEnvelopes + 1)
            .Select(position => Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), $"Area {position}", position)))
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/commands", batch, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var sync = await client.GetFromJsonAsync<JsonElement>("/api/sync?since=0", ct);
        var areas = sync.GetProperty("documents").TryGetProperty("areas", out var stored) ? stored.EnumerateArray() : default;
        Assert.DoesNotContain(areas, area => area.GetProperty("name").GetString()!.StartsWith("Area "));
    }

    [Fact]
    public async Task AWronglyTypedCommandMidBatchKeepsTheResponsesOfTheOthers()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var wronglyTyped = JsonSerializer.SerializeToElement(new
        {
            commandId = Guid.NewGuid(), userId = user, areaId = "not-a-guid", name = "Broken", position = 0
        });

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)),
            new CommandEnvelope(nameof(CreateArea), wronglyTyped),
            Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Work", 1))
        }, ct);

        response.EnsureSuccessStatusCode();
        var results = (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
        Assert.True(results[0].Accepted);
        Assert.False(results[1].Accepted);
        Assert.True(results[1].Unrecoverable);
        Assert.True(results[2].Accepted);
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
