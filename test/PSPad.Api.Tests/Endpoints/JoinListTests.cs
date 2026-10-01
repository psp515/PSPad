using System.Net;
using System.Net.Http.Json;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
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
                Guid.NewGuid(), ownerId, listId, token + "x", "Owner"));

        var response = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
