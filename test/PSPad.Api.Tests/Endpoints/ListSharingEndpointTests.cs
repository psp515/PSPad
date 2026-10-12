using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ListSharingEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task OnlyTheOwnerCanShareAList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (stranger, _) = await SignedIn(factory, ct);
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct);

        var response = await Sharing.ShareAsync(stranger, listId, Sharing.FreshToken(), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SharingAnUnknownListIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, _) = await SignedIn(factory, ct);

        var response = await Sharing.ShareAsync(owner, Guid.NewGuid(), Sharing.FreshToken(), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ATokenAlreadyInUseIsAConflictWithItsReason()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var other = await Sharing.SharedListAsync(owner, ownerId, ct);

        var response = await Sharing.ShareAsync(owner, other, token, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already in use", await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task StoppingSharingEndsTheLink()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (member, _) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);

        Assert.Equal(HttpStatusCode.NoContent, (await Sharing.StopSharingAsync(owner, listId, ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Sharing.JoinAsync(member, ct, token)).StatusCode);
    }

    [Fact]
    public async Task ARemovedMemberLosesTheList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (member, memberId) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await Sharing.RemoveMemberAsync(owner, listId, memberId, ct)).StatusCode);

        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.DoesNotContain(listId, sync!.MemberListIds ?? []);
    }

    [Fact]
    public async Task AMemberCannotRemoveSomeoneElse()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (member, memberId) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();

        var response = await Sharing.RemoveMemberAsync(member, listId, memberId, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMemberCanLeave()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (member, _) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await Sharing.LeaveAsync(member, listId, ct)).StatusCode);

        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.DoesNotContain(listId, sync!.MemberListIds ?? []);
    }

    [Fact]
    public async Task SomeoneWhoIsNotAMemberCannotLeave()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var (stranger, _) = await SignedIn(factory, ct);
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct);

        Assert.Equal(HttpStatusCode.NotFound, (await Sharing.LeaveAsync(stranger, listId, ct)).StatusCode);
    }

    [Fact]
    public async Task SharingCommandsNoLongerTravelThroughTheOutbox()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct);
        ICommand[] commands =
        [
            new ShareTaskList(Guid.NewGuid(), ownerId, listId, Sharing.FreshToken(), Sharing.Code, "Owner"),
            new StopSharingTaskList(Guid.NewGuid(), ownerId, listId),
            new RemoveListMember(Guid.NewGuid(), ownerId, listId, Guid.NewGuid()),
            new LeaveTaskList(Guid.NewGuid(), ownerId, listId)
        ];

        foreach (var command in commands)
        {
            var envelope = new CommandEnvelope(command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType()));
            var response = await owner.PostAsJsonAsync("/api/commands", new[] { envelope }, ct);
            var result = Assert.Single((await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!);

            Assert.False(result.Accepted);
            Assert.True(result.Unrecoverable);
        }
    }

    [Fact]
    public async Task ParallelWrongCodesStillCloseTheInviteAfterFive()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId) = await SignedIn(factory, ct);
        var token = Sharing.FreshToken();
        await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var guessers = new List<HttpClient>();
        for (var index = 0; index < TaskList.MostWrongCodes; index++)
        {
            guessers.Add((await SignedIn(factory, ct)).Client);
        }

        await Task.WhenAll(guessers.Select(guesser => Sharing.JoinAsync(guesser, ct, token, "AAAAAA")));

        var (late, _) = await SignedIn(factory, ct);
        Assert.Equal(HttpStatusCode.NotFound, (await Sharing.JoinAsync(late, ct, token)).StatusCode);
    }

    static async Task<(HttpClient Client, Guid UserId)> SignedIn(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        return (client, await Sharing.SignInAsync(client, ct));
    }
}
