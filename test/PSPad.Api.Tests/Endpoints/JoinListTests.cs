using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class JoinListTests(MongoFixture fixture)
{
    [Fact]
    public async Task JoiningReturnsTheListAndItsTasks()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Przeczytać"));

        var response = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinListResponse>(ct);
        Assert.Equal(listId, body!.ListId);
        var listRow = Assert.Single(body.Documents["tasklists"]);
        Assert.Equal(listId, listRow.GetProperty("id").GetGuid());
        var taskRow = Assert.Single(body.Documents["todotasks"]);
        Assert.Equal(taskId, taskRow.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task JoiningRecordsTheMembersName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        member.DefaultRequestHeaders.Add("X-Test-Name", "Anna");
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var response = await Sharing.JoinAsync(member, ct, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var listRow = Assert.Single(sync!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId);
        var member0 = Assert.Single(listRow.GetProperty("_members").EnumerateArray());
        Assert.Equal("Anna", member0.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task AnUnknownTokenIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var response = await Sharing.JoinAsync(member, ct, "nope-nope-nope-nope-nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ARotatedTokenNoLongerWorks()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.SendAsync(owner, ct,
            new ShareTaskList(
                Guid.NewGuid(), ownerId, listId, token + "x", Sharing.Code, "Owner"));

        var response = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ASecondListCannotTakeAnInviteTokenAlreadyInUse()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var thief = factory.ClientFor(Guid.NewGuid().ToString());
        var thiefId = await Sharing.SignInAsync(thief, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var areaId = Guid.NewGuid();
        var thiefListId = Guid.NewGuid();
        await Sharing.SendAsync(thief, ct,
            new CreateArea(Guid.NewGuid(), thiefId, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), thiefId, thiefListId, areaId, "Pułapka"));
        var envelope = new CommandEnvelope(nameof(ShareTaskList), JsonSerializer.SerializeToElement(
            new ShareTaskList(Guid.NewGuid(), thiefId, thiefListId, token, Sharing.Code, "Thief")));
        var hijack = await thief.PostAsJsonAsync("/api/commands", new[] { envelope }, ct);

        Assert.Equal(HttpStatusCode.OK, hijack.StatusCode);
        var rejected = Assert.Single((await hijack.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!);
        Assert.False(rejected.Accepted);
        Assert.Equal("That invite link is already in use.", rejected.Rejection);

        var response = await Sharing.JoinAsync(member, ct, token);
        var body = await response.Content.ReadFromJsonAsync<JoinListResponse>(ct);
        Assert.Equal(listId, body!.ListId);
    }

    [Fact]
    public async Task JoiningTwiceIsHarmless()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var first = await Sharing.JoinAsync(member, ct, token);
        var second = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var listRow = Assert.Single(sync!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId);
        Assert.Single(listRow.GetProperty("_members").EnumerateArray());
    }

    [Fact]
    public async Task JoiningADeletedListIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.SendAsync(owner, ct, new DeleteTaskList(Guid.NewGuid(), ownerId, listId));

        var response = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheOwnerJoiningTheirOwnListIsHarmless()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);

        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var response = await Sharing.JoinAsync(owner, ct, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var listRow = Assert.Single(sync!.Documents["tasklists"]);
        Assert.Empty(listRow.GetProperty("_members").EnumerateArray());
    }
}
