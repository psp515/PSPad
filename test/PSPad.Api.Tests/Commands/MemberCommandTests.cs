using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MemberCommandTests(MongoFixture fixture)
{
    [Fact]
    public async Task AMembersTaskBelongsToTheOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(member, ct, new CreateTask(Guid.NewGuid(), memberId, taskId, listId, "Dune"));

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var taskRow = Assert.Single(sync!.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == taskId);
        Assert.Equal(ownerId, taskRow.GetProperty("userId").GetGuid());

        var context = Persistence.TestContext.For(fixture);
        var eventRow = await context.Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", taskId))
            .FirstAsync(ct);
        Assert.Equal(ownerId, eventRow["userId"].AsGuid);
        var payload = JsonDocument.Parse(eventRow["payload"].AsString);
        Assert.Equal(memberId, payload.RootElement.GetProperty("ActorId").GetGuid());
    }

    [Fact]
    public async Task AMemberCompletesAndDeletesTheOwnersTask()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));

        await Sharing.SendAsync(member, ct, new CompleteTask(Guid.NewGuid(), memberId, taskId));
        await Sharing.SendAsync(member, ct, new DeleteTask(Guid.NewGuid(), memberId, taskId));
    }

    [Fact]
    public async Task AStrangersCommandIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await Sharing.SignInAsync(stranger, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));

        var response = await PostAsync(stranger, ct, new RenameTask(Guid.NewGuid(), strangerId, taskId, "Mine now"));

        var result = Assert.Single(response);
        Assert.False(result.Accepted);
        Assert.Equal("That list belongs to somebody else.", result.Rejection);
    }

    [Fact]
    public async Task ARemovedMembersCommandIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));

        await Sharing.SendAsync(owner, ct, new RemoveListMember(Guid.NewGuid(), ownerId, listId, memberId));

        var response = await PostAsync(member, ct, new RenameTask(Guid.NewGuid(), memberId, taskId, "Mine now"));

        var result = Assert.Single(response);
        Assert.False(result.Accepted);
        Assert.Equal("That list belongs to somebody else.", result.Rejection);
    }

    [Fact]
    public async Task AMemberCannotRenameTheList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var response = await PostAsync(member, ct, new RenameTaskList(Guid.NewGuid(), memberId, listId, "My list"));

        var result = Assert.Single(response);
        Assert.False(result.Accepted);
        Assert.Equal("That list belongs to somebody else.", result.Rejection);
    }

    [Fact]
    public async Task AMemberAddsAReferenceItem()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, kind: ListKind.Reference, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(
            member, ct, new CreateReferenceItem(Guid.NewGuid(), memberId, itemId, listId, "Gentleman's Gazette", 0));

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var itemRow = Assert.Single(
            sync!.Documents["referenceitems"], row => row.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(ownerId, itemRow.GetProperty("userId").GetGuid());
    }

    static async Task<CommandResponse[]> PostAsync(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
    }
}
