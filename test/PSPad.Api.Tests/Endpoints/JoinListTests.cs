using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
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

    [Fact]
    public async Task AWrongCodeAndAnUnknownTokenGetTheSameAnswer()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var wrongCode = await Sharing.JoinAsync(member, ct, token, "AAAAAA");
        var unknownToken = await Sharing.JoinAsync(member, ct, Sharing.FreshToken());

        Assert.Equal(HttpStatusCode.NotFound, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownToken.StatusCode);
        Assert.Equal(await unknownToken.Content.ReadAsStringAsync(ct), await wrongCode.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task ABlankCodeIs404AndCostsNoStrike()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var blank = await member.PostAsJsonAsync("/api/lists/join", new JoinListRequest(token, " "), ct);

        Assert.Equal(HttpStatusCode.NotFound, blank.StatusCode);
        var stored = await fixture.Database.GetCollection<BsonDocument>("tasklists")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", listId)).SingleAsync(ct);
        Assert.Equal(0, stored["wrongCodes"].AsInt32);
    }

    [Fact]
    public async Task FiveWrongCodesCloseTheInvite()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        for (var attempt = 0; attempt < TaskList.MostWrongCodes; attempt++)
        {
            await Sharing.JoinAsync(member, ct, token, "AAAAAA");
        }

        var right = await Sharing.JoinAsync(member, ct, token);
        Assert.Equal(HttpStatusCode.NotFound, right.StatusCode);
    }

    [Fact]
    public async Task AnExpiredInviteWithTheRightCodeAnswersGone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await ExpireInviteAsync(listId, ct);

        var response = await Sharing.JoinAsync(member, ct, token);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiredInviteWithAWrongCodeIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await ExpireInviteAsync(listId, ct);

        var response = await Sharing.JoinAsync(member, ct, token, "AAAAAA");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheEleventhAttemptInHalfAnHourIsThrottled()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);
        var someoneElse = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(someoneElse, ct);

        HttpResponseMessage last = null!;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            last = await Sharing.JoinAsync(member, ct, Sharing.FreshToken(), "AAAAAA");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        var other = await Sharing.JoinAsync(someoneElse, ct, Sharing.FreshToken(), "AAAAAA");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task AMemberNeverReceivesTheInviteSecrets()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        var joined = await Sharing.JoinAsync(member, ct, token);
        var body = await joined.Content.ReadFromJsonAsync<JoinListResponse>(ct);
        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var ownerSync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        AssertNoSecrets(Assert.Single(body!.Documents["tasklists"]));
        AssertNoSecrets(Assert.Single(sync!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId));
        var ownRow = Assert.Single(ownerSync!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId);
        Assert.Equal(token, ownRow.GetProperty("inviteToken").GetString());
        Assert.Equal(Sharing.Code, ownRow.GetProperty("inviteCode").GetString());
        Assert.Equal(JsonValueKind.String, ownRow.GetProperty("inviteExpiresAt").ValueKind);
    }

    static void AssertNoSecrets(JsonElement row)
    {
        Assert.Equal("Owner", row.GetProperty("ownerName").GetString());
        Assert.False(row.TryGetProperty("inviteToken", out var t) && t.ValueKind == JsonValueKind.String);
        Assert.False(row.TryGetProperty("inviteCode", out var c) && c.ValueKind == JsonValueKind.String);
        Assert.False(row.TryGetProperty("inviteExpiresAt", out var e) && e.ValueKind == JsonValueKind.String);
        Assert.False(row.TryGetProperty("wrongCodes", out _));
    }

    async Task ExpireInviteAsync(Guid listId, CancellationToken ct)
    {
        var lists = fixture.Database.GetCollection<BsonDocument>("tasklists");
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);
        await lists.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", listId),
            Builders<BsonDocument>.Update.Set("inviteExpiresAt", new BsonDocument
            {
                ["DateTime"] = past.UtcDateTime,
                ["Ticks"] = past.Ticks,
                ["Offset"] = 0
            }),
            cancellationToken: ct);
    }
}
