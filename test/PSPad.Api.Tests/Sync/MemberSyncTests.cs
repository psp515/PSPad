using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MemberSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task AMemberPullsTheSharedListAndItsTasks()
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
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Przeczytać"));

        await Sharing.JoinAsync(member, ct, token);

        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var listRow = Assert.Single(sync!.Documents["tasklists"]);
        Assert.Equal(listId, listRow.GetProperty("id").GetGuid());
        var taskRow = Assert.Single(sync.Documents["todotasks"]);
        Assert.Equal(taskId, taskRow.GetProperty("id").GetGuid());
        Assert.Equal([listId], sync.MemberListIds!);
    }

    [Fact]
    public async Task AMemberPullsLaterEditsByMarker()
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
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Przeczytać"));
        await Sharing.JoinAsync(member, ct, token);

        var first = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        await Sharing.SendAsync(owner, ct, new RenameTask(Guid.NewGuid(), ownerId, taskId, "Przeczytać dziś"));

        var second = await member.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);

        var taskRow = Assert.Single(second!.Documents["todotasks"]);
        Assert.Equal(taskId, taskRow.GetProperty("id").GetGuid());
        Assert.Equal("Przeczytać dziś", taskRow.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AStrangerSeesNothingOfTheList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(stranger, ct);

        var listId = await Sharing.SharedListAsync(owner, ownerId, ct);
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, Guid.NewGuid(), listId, "Przeczytać"));

        var sync = await stranger.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Documents["tasklists"]);
        Assert.Empty(sync.Documents["todotasks"]);
        Assert.Empty(sync.MemberListIds ?? []);
    }

    [Fact]
    public async Task FullReturnsTheWholeListBelowTheMarker()
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
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Przeczytać"));
        await Sharing.JoinAsync(member, ct, token);

        var first = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var full = await member.GetFromJsonAsync<SyncResponse>(
            $"/api/sync?since={first!.Marker}&full={listId}", ct);

        var listRow = Assert.Single(full!.Documents["tasklists"]);
        Assert.Equal(listId, listRow.GetProperty("id").GetGuid());
        var taskRow = Assert.Single(full.Documents["todotasks"]);
        Assert.Equal(taskId, taskRow.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task FullIgnoresListsTheCallerCannotSee()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(stranger, ct);

        var listId = await Sharing.SharedListAsync(owner, ownerId, ct);
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, Guid.NewGuid(), listId, "Przeczytać"));

        var sync = await stranger.GetFromJsonAsync<SyncResponse>($"/api/sync?since=0&full={listId}", ct);

        Assert.Empty(sync!.Documents["tasklists"]);
        Assert.Empty(sync.Documents["todotasks"]);
    }

    [Fact]
    public async Task ARemovedMemberNoLongerHasTheListInTheSet()
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

        await Sharing.SendAsync(owner, ct, new RemoveListMember(Guid.NewGuid(), ownerId, listId, memberId));

        var sync = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.MemberListIds ?? []);
    }

    [Fact]
    public async Task ATaskMovedOutOfASharedListReachesTheMemberOnceMore()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var sharedId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var privateId = await PrivateListAsync(owner, ownerId, ListKind.Tasks, ct);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, taskId, sharedId, "Przeczytać"));
        await Sharing.JoinAsync(member, ct, token);
        var first = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        await Sharing.SendAsync(owner, ct,
            new SetTaskDescription(Guid.NewGuid(), ownerId, taskId, "Rozdział 3"),
            new MoveTaskToList(Guid.NewGuid(), ownerId, taskId, privateId),
            new RenameTask(Guid.NewGuid(), ownerId, taskId, "Prywatne"));

        var second = await member.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);
        var taskRow = Assert.Single(second!.Documents["todotasks"]);
        Assert.Equal(taskId, taskRow.GetProperty("id").GetGuid());
        Assert.Equal(privateId, taskRow.GetProperty("listId").GetGuid());
        Assert.DoesNotContain("Prywatne", taskRow.GetRawText());
        Assert.DoesNotContain("Rozdział", taskRow.GetRawText());
        Assert.DoesNotContain("Przeczytać", taskRow.GetRawText());
        Assert.DoesNotContain(second.Documents["tasklists"], row => row.GetProperty("id").GetGuid() == privateId);
    }

    [Fact]
    public async Task AReferenceItemMovedOutOfASharedListReachesTheMemberOnceMore()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var sharedId = await Sharing.SharedListAsync(owner, ownerId, ct, ListKind.Reference, token);
        var privateId = await PrivateListAsync(owner, ownerId, ListKind.Reference, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateReferenceItem(Guid.NewGuid(), ownerId, itemId, sharedId, "PLA", 0));
        await Sharing.JoinAsync(member, ct, token);
        var first = await member.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        await Sharing.SendAsync(owner, ct,
            new SetReferenceItemDescription(Guid.NewGuid(), ownerId, itemId, "Sucha szpula"),
            new MoveReferenceItemToList(Guid.NewGuid(), ownerId, itemId, privateId),
            new RenameReferenceItem(Guid.NewGuid(), ownerId, itemId, "Prywatne"));

        var second = await member.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);
        var itemRow = Assert.Single(second!.Documents["referenceitems"]);
        Assert.Equal(itemId, itemRow.GetProperty("id").GetGuid());
        Assert.Equal(privateId, itemRow.GetProperty("listId").GetGuid());
        Assert.DoesNotContain("Prywatne", itemRow.GetRawText());
        Assert.DoesNotContain("Sucha", itemRow.GetRawText());
        Assert.DoesNotContain("PLA", itemRow.GetRawText());
    }

    static async Task<Guid> PrivateListAsync(HttpClient owner, Guid ownerId, ListKind kind, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new Module.Tasks.Areas.CreateArea(Guid.NewGuid(), ownerId, areaId, "Prywatne", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Moje", kind));
        return listId;
    }
}
