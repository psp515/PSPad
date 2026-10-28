using System.Net;
using System.Net.Http.Json;
using PSPad.Api.Tests.Sync;
using PSPad.Api.Snapshots;
using PSPad.Contracts;
using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class LiveEndpointTests(MongoFixture fixture)
{
    static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    ApiFactory Factory(params (string Key, string Value)[] settings) =>
        new(fixture, configuration: settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value));

    [Fact]
    public async Task TheStreamOpensWithReadyAndHeadersProxiesPassThrough()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(client, ct);

        await using var live = await LiveListener.OpenAsync(client, ct);

        Assert.Equal("text/event-stream", live.Response.Content.Headers.ContentType?.MediaType);
        Assert.True(live.Response.Headers.CacheControl?.NoCache);
        Assert.True(live.Response.Headers.CacheControl?.NoTransform);
        Assert.Equal("no", Assert.Single(live.Response.Headers.GetValues("X-Accel-Buffering")));
    }

    [Fact]
    public async Task AUsersOwnCommandNudgesTheirOtherDevice()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var subject = Guid.NewGuid().ToString();
        var phone = factory.ClientFor(subject);
        var userId = await Sharing.SignInAsync(phone, ct);
        await using var live = await LiveListener.OpenAsync(phone, ct);

        await Sharing.SendAsync(factory.ClientFor(subject), ct, new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), "Dom", 0));

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task AMembersEditNudgesTheOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(owner, ct);

        await Sharing.SendAsync(member, ct, new CreateTask(Guid.NewGuid(), memberId, Guid.NewGuid(), listId, "Diuna"));

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task ASnapshotMarkNudgesTheOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Groceries", ListKind.Tasks),
            new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Buy milk"));
        var published = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(1)), ct);
        var snapshot = (await published.Content.ReadFromJsonAsync<PublishedSnapshotView>(ct))!;
        await using var live = await LiveListener.OpenAsync(owner, ct);

        (await factory.CreateClient().PostAsJsonAsync(
            $"/api/public/snapshots/{snapshot.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct)).EnsureSuccessStatusCode();

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task ARemovedMemberIsNudged()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(member, ct);

        (await Sharing.RemoveMemberAsync(owner, listId, memberId, ct)).EnsureSuccessStatusCode();

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task AStrangerHearsOnlyHeartbeats()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory(("Live:Heartbeat", "00:00:00.300"));
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(stranger, ct);
        await using var live = await LiveListener.OpenAsync(stranger, ct);

        await Sharing.SendAsync(owner, ct, new CreateArea(Guid.NewGuid(), ownerId, Guid.NewGuid(), "Dom", 0));

        Assert.Equal(PSPad.App.Live.SseReader.Heartbeat, await live.NextAsync(Soon, skipHeartbeats: false));
        Assert.True(await live.StaysQuietAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task TheStreamEndsAtItsMaximumLife()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory(("Live:MaxStream", "00:00:01"));
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(client, ct);
        await using var live = await LiveListener.OpenAsync(client, ct);

        Assert.True(await live.EndsWithinAsync(Soon));
    }

    [Fact]
    public async Task AStreamPastTheCapIsRefused()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory(("Live:MaxStreamsPerUser", "1"));
        var subject = Guid.NewGuid().ToString();
        var client = factory.ClientFor(subject);
        await Sharing.SignInAsync(client, ct);
        await using var first = await LiveListener.OpenAsync(client, ct);

        using var second = await factory.ClientFor(subject).SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/live"), HttpCompletionOption.ResponseHeadersRead, ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task ClosingAStreamFreesItsPlaceForAnother()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory(("Live:MaxStreamsPerUser", "1"));
        var subject = Guid.NewGuid().ToString();
        var client = factory.ClientFor(subject);
        await Sharing.SignInAsync(client, ct);
        var first = await LiveListener.OpenAsync(client, ct);
        await first.DisposeAsync();

        await using var second = await OpenWhenFreedAsync(factory.ClientFor(subject), ct);

        Assert.Equal("text/event-stream", second.Response.Content.Headers.ContentType?.MediaType);
    }

    static async Task<LiveListener> OpenWhenFreedAsync(HttpClient client, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            while (true)
            {
                try
                {
                    return await LiveListener.OpenAsync(client, deadline.Token);
                }
                catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Assert.Fail("slot never freed");
            throw;
        }
    }

    [Fact]
    public async Task AnAnonymousCallerIsTurnedAway()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = Factory();

        using var response = await factory.CreateClient().GetAsync("/api/live", HttpCompletionOption.ResponseHeadersRead, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
