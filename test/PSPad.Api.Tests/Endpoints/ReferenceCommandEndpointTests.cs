using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Identity;
using PSPad.Api.Tests.Persistence;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ReferenceCommandEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task EveryReferenceCommandIsAcceptedOverTheWire()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var colourField = Guid.NewGuid();
        var weightField = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference)),
            Envelope(new CreateReferenceItem(Guid.NewGuid(), user, itemId, listId, "PLA Black", 0)),
            Envelope(new RenameReferenceItem(Guid.NewGuid(), user, itemId, "PLA Jet Black")),
            Envelope(new SetReferenceItemDescription(Guid.NewGuid(), user, itemId, "Dry 4h at 50 °C")),
            Envelope(new StarReferenceItem(Guid.NewGuid(), user, itemId, true)),
            Envelope(new AddReferenceField(Guid.NewGuid(), user, itemId, colourField, "Colour", "Black", null)),
            Envelope(new AddReferenceField(Guid.NewGuid(), user, itemId, weightField, "Left", "350 g", "quantity")),
            Envelope(new EditReferenceField(Guid.NewGuid(), user, itemId, colourField, "Colour", "Jet black", null)),
            Envelope(new MoveReferenceField(Guid.NewGuid(), user, itemId, weightField, 0)),
            Envelope(new RemoveReferenceField(Guid.NewGuid(), user, itemId, colourField))
        }, ct);

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);

        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }

    [Fact]
    public async Task ATaskSentToAReferenceListIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference)),
            Envelope(new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "Buy filament"))
        }, ct);

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);

        Assert.True(results![0].Accepted, results[0].Rejection);
        Assert.True(results[1].Accepted, results[1].Rejection);
        Assert.False(results[2].Accepted);
        Assert.NotNull(results[2].Rejection);
    }

    [Fact]
    public async Task AnInboxItemOrganisedIntoAReferenceListIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference)),
            Envelope(new CreateInbox(Guid.NewGuid(), user, inboxId)),
            Envelope(new CaptureToInbox(Guid.NewGuid(), user, inboxId, itemId, "Buy filament")),
            Envelope(new OrganiseInboxItem(Guid.NewGuid(), user, inboxId, itemId, listId, Guid.NewGuid()))
        }, ct);

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);

        Assert.True(results![0].Accepted, results[0].Rejection);
        Assert.True(results[1].Accepted, results[1].Rejection);
        Assert.True(results[2].Accepted, results[2].Rejection);
        Assert.True(results[3].Accepted, results[3].Rejection);
        Assert.False(results[4].Accepted);
        Assert.NotNull(results[4].Rejection);
    }

    [Fact]
    public async Task DeletingAReferenceListDeletesItsItems()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference),
            new CreateReferenceItem(Guid.NewGuid(), user, itemId, listId, "PLA Black", 0));

        await Send(client, ct, new DeleteTaskList(Guid.NewGuid(), user, listId));

        var context = Persistence.TestContext.For(fixture);
        var stored = await context.Collection<BsonDocument>("referenceitems")
            .Find(Builders<BsonDocument>.Filter.Eq(
                "_id", new BsonBinaryData(itemId, GuidRepresentation.Standard)))
            .SingleAsync(ct);
        Assert.True(stored["deleted"].AsBoolean);

        var events = await context.Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq(
                "aggregateId", new BsonBinaryData(itemId, GuidRepresentation.Standard)))
            .ToListAsync(ct);
        Assert.Contains(events, entry => entry["type"].AsString == nameof(ReferenceItemDeleted));
    }

    [Fact]
    public async Task DeletingTheAccountRemovesReferenceItems()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", ListKind.Reference),
            new CreateReferenceItem(Guid.NewGuid(), user, Guid.NewGuid(), listId, "PLA Black", 0));

        var context = Persistence.TestContext.For(fixture);
        var beforeWipe = await context.Collection<BsonDocument>("referenceitems")
            .Find(Builders<BsonDocument>.Filter.Eq(
                "userId", new BsonBinaryData(user, GuidRepresentation.Standard)))
            .CountDocumentsAsync(ct);
        Assert.Equal(1, beforeWipe);

        var response = await client.DeleteAsync("/api/account", ct);
        response.EnsureSuccessStatusCode();

        var remaining = await context.Collection<BsonDocument>("referenceitems")
            .Find(Builders<BsonDocument>.Filter.Eq(
                "userId", new BsonBinaryData(user, GuidRepresentation.Standard)))
            .CountDocumentsAsync(ct);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task SetTaskDescriptionIsAcceptedAndStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Chores"),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Read a book"));

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskDescription(Guid.NewGuid(), user, taskId, "# Notes\n\nBring `bookmark`."))
        }, ct);
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.True(Assert.Single(results!).Accepted);

        var context = Persistence.TestContext.For(fixture);
        var loaded = await new MongoDocumentStore<TodoTask>(context).LoadAsync(taskId, ct);
        Assert.Equal("# Notes\n\nBring `bookmark`.", loaded!.Description);
    }

    static async Task Send(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands.Select(Envelope).ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }

    static CommandEnvelope Envelope(object command) =>
        new(command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType()));
}
