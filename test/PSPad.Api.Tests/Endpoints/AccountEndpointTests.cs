using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Identity;
using PSPad.Api.Tests.Persistence;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class AccountEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task DeletingTheAccountRemovesEveryDocumentForThatUser()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userId = me!.UserId;

        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(
                nameof(CreateArea),
                JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), "Home", 0)))
        }, ct);

        var response = await client.DeleteAsync("/api/account", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>(ct);

        Assert.True(result!.KeycloakRemoved);
        Assert.Equal([subject], keycloak.DeletedSubjects);

        var context = Persistence.TestContext.For(fixture);
        var collectionNames = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct))
            .ToListAsync(ct);

        foreach (var name in collectionNames)
        {
            var remaining = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
                .CountDocumentsAsync(ct);

            Assert.True(remaining == 0, $"Collection '{name}' still has {remaining} document(s) for the deleted user.");
        }
    }

    [Fact]
    public async Task DeletingTheAccountRemovesItsAreaViews()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, new FakeKeycloakAdminClient(succeeds: true));
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var userId = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var list = Guid.NewGuid();
        var reorder = new ReorderLists(Guid.NewGuid(), userId, Guid.NewGuid(), [list], list, 0);

        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(ReorderLists), JsonSerializer.SerializeToElement(reorder))
        }, ct);

        var context = Persistence.TestContext.For(fixture);
        var views = context.Collection<BsonDocument>("areaviews");
        Assert.Equal(1, await views.Find(Builders<BsonDocument>.Filter.Eq("userId", userId)).CountDocumentsAsync(ct));

        (await client.DeleteAsync("/api/account", ct)).EnsureSuccessStatusCode();

        Assert.Equal(0, await views.Find(Builders<BsonDocument>.Filter.Eq("userId", userId)).CountDocumentsAsync(ct));
    }

    [Fact]
    public async Task AKeycloakFailureStillReportsSuccessBecauseTheDataIsAlreadyGone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: false);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userId = me!.UserId;

        var response = await client.DeleteAsync("/api/account", ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>(ct);
        Assert.False(result!.KeycloakRemoved);

        var context = Persistence.TestContext.For(fixture);
        var remainingUsers = await context.Collection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
            .CountDocumentsAsync(ct);
        Assert.Equal(0, remainingUsers);
    }

    [Fact]
    public async Task DeletingOneUsersAccountNeverTouchesAnotherUsersData()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subjectA = Guid.NewGuid().ToString();
        var subjectB = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);

        var clientA = factory.ClientFor(subjectA);
        var meA = await clientA.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userIdA = meA!.UserId;

        var clientB = factory.ClientFor(subjectB);
        var meB = await clientB.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userIdB = meB!.UserId;

        await clientA.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(
                nameof(CreateArea),
                JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), userIdA, Guid.NewGuid(), "A's area", 0)))
        }, ct);

        await clientB.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(
                nameof(CreateArea),
                JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), userIdB, Guid.NewGuid(), "B's area", 0)))
        }, ct);

        var context = Persistence.TestContext.For(fixture);
        var collectionNames = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct))
            .ToListAsync(ct);

        var countsForBBeforeDeletion = await CountsPerCollectionAsync(context, collectionNames, userIdB, ct);

        var deleteResponse = await clientA.DeleteAsync("/api/account", ct);
        deleteResponse.EnsureSuccessStatusCode();

        foreach (var name in collectionNames)
        {
            var remainingForA = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", userIdA))
                .CountDocumentsAsync(ct);

            Assert.True(remainingForA == 0, $"Collection '{name}' still has {remainingForA} document(s) for the deleted user A.");
        }

        var countsForBAfterDeletion = await CountsPerCollectionAsync(context, collectionNames, userIdB, ct);
        Assert.Equal(countsForBBeforeDeletion, countsForBAfterDeletion);
        Assert.Contains(countsForBAfterDeletion, pair => pair.Value > 0);

        var meBAgain = await clientB.GetFromJsonAsync<MeResponse>("/api/me", ct);
        Assert.Equal(userIdB, meBAgain!.UserId);
    }

    [Fact]
    public async Task AnUnanticipatedKeycloakExceptionStillReturns200WithKeycloakRemovedFalse()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true, throws: new InvalidOperationException("boom"));
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        await client.GetFromJsonAsync<MeResponse>("/api/me", ct);

        var response = await client.DeleteAsync("/api/account", ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>(ct);
        Assert.False(result!.KeycloakRemoved);
    }

    [Fact]
    public async Task DeletingTwiceIsHarmless()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        await client.GetFromJsonAsync<MeResponse>("/api/me", ct);

        await client.DeleteAsync("/api/account", ct);
        var second = await client.DeleteAsync("/api/account", ct);

        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeletingAMembersAccountRemovesThemFromSharedLists()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, new FakeKeycloakAdminClient(succeeds: true));
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        (await member.DeleteAsync("/api/account", ct)).EnsureSuccessStatusCode();

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var listRow = Assert.Single(sync!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId);
        Assert.Empty(listRow.GetProperty("_members").EnumerateArray());
    }

    [Fact]
    public async Task TheOwnersNextPullSeesTheMembershipChange()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, new FakeKeycloakAdminClient(succeeds: true));
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var afterJoin = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        (await member.DeleteAsync("/api/account", ct)).EnsureSuccessStatusCode();

        var afterDeletion = await owner.GetFromJsonAsync<SyncResponse>($"/api/sync?since={afterJoin!.Marker}", ct);

        var listRow = Assert.Single(
            afterDeletion!.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == listId);
        Assert.Empty(listRow.GetProperty("_members").EnumerateArray());
    }

    [Fact]
    public async Task DeletingAnOwnersAccountLeavesMembersWithNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture, new FakeKeycloakAdminClient(succeeds: true));
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        (await owner.DeleteAsync("/api/account", ct)).EnsureSuccessStatusCode();

        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Documents["tasklists"]);
        Assert.Empty(sync.MemberListIds ?? []);
    }

    static async Task<Dictionary<string, long>> CountsPerCollectionAsync(
        MongoContext context, IReadOnlyList<string> collectionNames, Guid userId, CancellationToken ct)
    {
        var counts = new Dictionary<string, long>();

        foreach (var name in collectionNames)
        {
            counts[name] = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
                .CountDocumentsAsync(ct);
        }

        return counts;
    }
}
