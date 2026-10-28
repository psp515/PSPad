using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
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

        (await Sharing.RemoveMemberAsync(owner, listId, memberId, ct)).EnsureSuccessStatusCode();

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

    static async Task<(HttpClient Owner, Guid OwnerId, HttpClient Member, Guid MemberId, Guid ListId)> SharedAsync(
        ApiFactory factory, ListKind kind, CancellationToken ct)
    {
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, kind, token);
        await Sharing.JoinAsync(member, ct, token);
        return (owner, ownerId, member, memberId, listId);
    }

    static async Task<Guid> OwnListAsync(HttpClient client, Guid user, ListKind kind, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Sharing.SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Mine", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Member's list", kind));
        return listId;
    }

    [Fact]
    public async Task AMemberRenamesTheOwnersTaskAndItStaysTheOwners()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ListKind.Tasks, ct);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));

        await Sharing.SendAsync(member, ct, new RenameTask(Guid.NewGuid(), memberId, taskId, "Dune Messiah"));

        var row = Assert.Single(await Commands.SyncedAsync(owner, "todotasks", ct));
        Assert.Equal("Dune Messiah", row.GetProperty("name").GetString());
        Assert.Equal(ownerId, row.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task AMemberCannotMoveATaskIntoTheirOwnList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ListKind.Tasks, ct);
        var membersList = await OwnListAsync(member, memberId, ListKind.Tasks, ct);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Dune"));

        var result = Assert.Single(await PostAsync(member, ct, new MoveTaskToList(Guid.NewGuid(), memberId, taskId, membersList)));

        Assert.False(result.Accepted);
        Assert.Equal("A task can only move between lists of the same owner.", result.Rejection);
        var row = Assert.Single(await Commands.SyncedAsync(owner, "todotasks", ct));
        Assert.Equal(listId, row.GetProperty("listId").GetGuid());
    }

    [Fact]
    public async Task OrganisingIntoASharedListCreatesTheOwnersTask()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ListKind.Tasks, ct);
        var inboxId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(member, ct,
            new CreateInbox(Guid.NewGuid(), memberId, inboxId),
            new CaptureToInbox(Guid.NewGuid(), memberId, inboxId, itemId, "Read Dune"));

        await Sharing.SendAsync(member, ct, new OrganiseInboxItem(Guid.NewGuid(), memberId, inboxId, itemId, listId, taskId));

        var task = Assert.Single(await Commands.SyncedAsync(owner, "todotasks", ct));
        Assert.Equal(taskId, task.GetProperty("id").GetGuid());
        Assert.Equal(ownerId, task.GetProperty("userId").GetGuid());
        var inbox = Assert.Single(await Commands.SyncedAsync(member, "inboxes", ct), row => row.GetProperty("id").GetGuid() == inboxId);
        Assert.Equal(memberId, inbox.GetProperty("userId").GetGuid());
        Assert.Empty(inbox.GetProperty("_items").EnumerateArray());
    }

    [Fact]
    public async Task AMemberRenamesTheOwnersReferenceItemAndItStaysTheOwners()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ListKind.Reference, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateReferenceItem(Guid.NewGuid(), ownerId, itemId, listId, "PLA Black", 0));

        await Sharing.SendAsync(member, ct, new RenameReferenceItem(Guid.NewGuid(), memberId, itemId, "PETG Grey"));

        var row = Assert.Single(await Commands.SyncedAsync(owner, "referenceitems", ct));
        Assert.Equal("PETG Grey", row.GetProperty("name").GetString());
        Assert.Equal(ownerId, row.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task AStrangerCannotRenameAReferenceItemOfASharedList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, _, _, listId) = await SharedAsync(factory, ListKind.Reference, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await Sharing.SignInAsync(stranger, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateReferenceItem(Guid.NewGuid(), ownerId, itemId, listId, "PLA Black", 0));

        var result = Assert.Single(await PostAsync(stranger, ct, new RenameReferenceItem(Guid.NewGuid(), strangerId, itemId, "PETG Grey")));

        Assert.False(result.Accepted);
        Assert.Equal("PLA Black", Assert.Single(await Commands.SyncedAsync(owner, "referenceitems", ct)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task AMemberCannotMoveAnItemIntoTheirOwnList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, member, memberId, listId) = await SharedAsync(factory, ListKind.Reference, ct);
        var membersList = await OwnListAsync(member, memberId, ListKind.Reference, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateReferenceItem(Guid.NewGuid(), ownerId, itemId, listId, "PLA Black", 0));

        var result = Assert.Single(await PostAsync(member, ct, new MoveReferenceItemToList(Guid.NewGuid(), memberId, itemId, membersList)));

        Assert.False(result.Accepted);
        Assert.Equal("An item can only move between lists of the same owner.", result.Rejection);
        Assert.Equal(listId, Assert.Single(await Commands.SyncedAsync(owner, "referenceitems", ct)).GetProperty("listId").GetGuid());
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
