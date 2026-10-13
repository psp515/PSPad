using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BudgetSharingEndpointTests(MongoFixture fixture)
{
    sealed record Pair(ApiFactory Factory, HttpClient Owner, Guid OwnerId, HttpClient Member, Guid MemberId);

    async Task<Pair> PairAsync(CancellationToken ct, ApiFactory? factory = null)
    {
        factory ??= new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        member.DefaultRequestHeaders.Add("X-Test-Name", "Anna");
        var memberId = await Sharing.SignInAsync(member, ct);
        return new Pair(factory, owner, ownerId, member, memberId);
    }

    static Money Pln(decimal amount) => new(amount, "PLN", 1m, new DateOnly(2026, 10, 9));

    static RecordExpense Expense(Guid userId, Guid budgetId, Guid? entryId = null) =>
        new(Guid.NewGuid(), userId, entryId ?? Guid.NewGuid(), budgetId, "Bread", "Food", Pln(5), new DateOnly(2026, 10, 9), null);

    async Task<Guid> JoinedBudgetAsync(Pair pair, CancellationToken ct)
    {
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(pair.Owner, pair.OwnerId, ct, token);
        (await BudgetSharing.JoinAsync(pair.Member, ct, token)).EnsureSuccessStatusCode();
        return budgetId;
    }

    [Fact]
    public async Task JoiningReturnsTheBudgetItsEntriesAndSnapshots()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(pair.Owner, pair.OwnerId, ct, token);
        var entryId = Guid.NewGuid();
        await Sharing.SendAsync(pair.Owner, ct,
            Expense(pair.OwnerId, budgetId, entryId),
            new StartBalanceMonth(Guid.NewGuid(), pair.OwnerId, budgetId, new YearMonth(2026, 10)));

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JoinBudgetResponse>(ct);
        Assert.Equal(budgetId, body!.BudgetId);
        var budgetRow = Assert.Single(body.Documents["budgets"]);
        Assert.False(budgetRow.TryGetProperty("inviteToken", out _));
        Assert.False(budgetRow.TryGetProperty("inviteCode", out _));
        Assert.Equal("Anna", budgetRow.GetProperty("_members")[0].GetProperty("displayName").GetString());
        Assert.Equal(entryId, Assert.Single(body.Documents["moneyentries"]).GetProperty("id").GetGuid());
        Assert.Single(body.Documents["balancesnapshots"]);
    }

    [Fact]
    public async Task AWrongCodeIs404AndCounts()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(pair.Owner, pair.OwnerId, ct, token);

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, token, "AAAAAA");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = await Budgets().Find(Builders<BsonDocument>.Filter.Eq("_id", budgetId)).FirstAsync(ct);
        Assert.Equal(1, stored["wrongCodes"].AsInt32);
    }

    [Fact]
    public async Task AnExpiredInviteIs410()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(pair.Owner, pair.OwnerId, ct, token);
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);
        await Budgets().UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", budgetId),
            Builders<BsonDocument>.Update.Set("inviteExpiresAt",
                new BsonDocument { ["DateTime"] = past.UtcDateTime, ["Ticks"] = past.Ticks, ["Offset"] = 0 }),
            cancellationToken: ct);

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, token);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownTokenIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, Sharing.FreshToken());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchivingEndsTheInvite()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(pair.Owner, pair.OwnerId, ct, token);
        await Sharing.SendAsync(pair.Owner, ct, new ArchiveBudget(Guid.NewGuid(), pair.OwnerId, budgetId));

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheOwnerShares()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);

        Assert.Equal(HttpStatusCode.NotFound,
            (await BudgetSharing.ShareAsync(pair.Member, budgetId, Sharing.FreshToken(), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await BudgetSharing.StopSharingAsync(pair.Member, budgetId, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await BudgetSharing.RemoveMemberAsync(pair.Member, budgetId, pair.MemberId, ct)).StatusCode);
    }

    [Fact]
    public async Task SharingCommandsCannotComeThroughTheOutbox()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(pair.Owner, ct, new CreateBudget(Guid.NewGuid(), pair.OwnerId, budgetId, "Household"));
        var command = new ShareBudget(Guid.NewGuid(), pair.OwnerId, budgetId, Sharing.FreshToken(), Sharing.Code, "L");

        var response = await pair.Owner.PostAsJsonAsync("/api/commands",
            new[] { new CommandEnvelope(nameof(ShareBudget), JsonSerializer.SerializeToElement(command)) }, ct);

        var result = Assert.Single((await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!);
        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
    }

    [Fact]
    public async Task AMembersExpenseBelongsToTheOwnerAndRecordsTheActor()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);
        var entryId = Guid.NewGuid();

        await Sharing.SendAsync(pair.Member, ct, Expense(pair.MemberId, budgetId, entryId));

        var sync = await pair.Owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var row = Assert.Single(sync!.Documents["moneyentries"], entry => entry.GetProperty("id").GetGuid() == entryId);
        Assert.Equal(pair.OwnerId, row.GetProperty("userId").GetGuid());
        var eventRow = await fixture.Database.GetCollection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", entryId)).FirstAsync(ct);
        Assert.Equal(pair.OwnerId, eventRow["userId"].AsGuid);
        Assert.Equal(pair.MemberId,
            JsonDocument.Parse(eventRow["payload"].AsString).RootElement.GetProperty("ActorId").GetGuid());
    }

    [Fact]
    public async Task AMemberSyncsTheBudgetWithoutTheInviteAndItsContent()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);
        var entryId = Guid.NewGuid();
        await Sharing.SendAsync(pair.Owner, ct, Expense(pair.OwnerId, budgetId, entryId));

        var sync = await pair.Member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var budgetRow = Assert.Single(sync!.Documents["budgets"]);
        Assert.False(budgetRow.TryGetProperty("inviteToken", out _));
        Assert.Equal(entryId, Assert.Single(sync.Documents["moneyentries"]).GetProperty("id").GetGuid());
        Assert.Equal([budgetId], sync.MemberBudgetIds!);
        var ownerSync = await pair.Owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.Empty(ownerSync!.MemberBudgetIds!);
        Assert.True(Assert.Single(ownerSync.Documents["budgets"]).TryGetProperty("inviteToken", out _));
    }

    [Fact]
    public async Task AFullPullReturnsTheWholeBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);
        await Sharing.SendAsync(pair.Owner, ct, Expense(pair.OwnerId, budgetId));
        var marker = (await pair.Member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        var sync = await pair.Member.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}&full={budgetId}", ct);

        Assert.Single(sync!.Documents["budgets"]);
        Assert.Single(sync.Documents["moneyentries"]);
    }

    [Fact]
    public async Task LeavingAndRemovalEndAccess()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);

        Assert.Equal(HttpStatusCode.NoContent, (await BudgetSharing.LeaveAsync(pair.Member, budgetId, ct)).StatusCode);
        var afterLeaving = await pair.Member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.Empty(afterLeaving!.MemberBudgetIds!);

        var token = Sharing.FreshToken();
        (await BudgetSharing.ShareAsync(pair.Owner, budgetId, token, ct)).EnsureSuccessStatusCode();
        (await BudgetSharing.JoinAsync(pair.Member, ct, token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent,
            (await BudgetSharing.RemoveMemberAsync(pair.Owner, budgetId, pair.MemberId, ct)).StatusCode);

        var envelopes = new[] { new CommandEnvelope(nameof(RecordExpense),
            JsonSerializer.SerializeToElement(Expense(pair.MemberId, budgetId))) };
        var results = await (await pair.Member.PostAsJsonAsync("/api/commands", envelopes, ct))
            .Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        var result = Assert.Single(results!);
        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
    }

    [Fact]
    public async Task DeletingAMembersAccountRemovesThemFromSharedBudgets()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct, new ApiFactory(fixture, new Identity.FakeKeycloakAdminClient(succeeds: true)));
        await using var factory = pair.Factory;
        var budgetId = await JoinedBudgetAsync(pair, ct);
        var before = (await pair.Owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct))!.Marker;

        (await pair.Member.DeleteAsync("/api/account", ct)).EnsureSuccessStatusCode();

        var sync = await pair.Owner.GetFromJsonAsync<SyncResponse>($"/api/sync?since={before}", ct);
        var row = Assert.Single(sync!.Documents["budgets"], budget => budget.GetProperty("id").GetGuid() == budgetId);
        Assert.Empty(row.GetProperty("_members").EnumerateArray());
    }

    [Fact]
    public async Task ListAndBudgetJoinsShareOneLimit()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var pair = await PairAsync(ct);
        await using var factory = pair.Factory;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Sharing.JoinAsync(pair.Member, ct, Sharing.FreshToken(), "AAAAAA");
        }

        var response = await BudgetSharing.JoinAsync(pair.Member, ct, Sharing.FreshToken(), "AAAAAA");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    IMongoCollection<BsonDocument> Budgets() => fixture.Database.GetCollection<BsonDocument>("budgets");
}
