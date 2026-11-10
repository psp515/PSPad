using MongoDB.Driver;
using PSPad.Module.Presentation.UserViews;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class UserViewSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task ShowingFinishedTasksSyncs()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);

        await SendAsync(client, ct, new ShowFinishedOnSchedule(Guid.NewGuid(), user, true));

        var row = Assert.Single(await SyncedAsync(client, "userviews", ct));
        Assert.Equal(UserView.IdFor(user), row.GetProperty("id").GetGuid());
        Assert.True(row.GetProperty("showFinishedOnSchedule").GetBoolean());
    }

    [Fact]
    public async Task AnotherUsersViewNeverSyncs()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var other = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(other, ct);

        await SendAsync(owner, ct, new ShowFinishedOnSchedule(Guid.NewGuid(), ownerId, true));

        Assert.Empty(await SyncedAsync(other, "userviews", ct));
    }

    [Fact]
    public async Task HidingFinishedTasksWithNoViewStoresAndSyncsNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);

        await SendAsync(client, ct, new ShowFinishedOnSchedule(Guid.NewGuid(), user, false));

        Assert.Empty(await SyncedAsync(client, "userviews", ct));
        var stored = await Persistence.TestContext.For(fixture).Collection<UserView>()
            .CountDocumentsAsync(Builders<UserView>.Filter.Eq(view => view.Id, UserView.IdFor(user)), cancellationToken: ct);
        Assert.Equal(0, stored);
    }
}
