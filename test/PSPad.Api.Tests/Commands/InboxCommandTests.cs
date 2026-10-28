using System.Text.Json;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Commands;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class InboxCommandTests(MongoFixture fixture)
{
    static async Task<JsonElement[]> InboxItemsAsync(HttpClient client, Guid inboxId, CancellationToken ct) =>
        Assert.Single(await SyncedAsync(client, "inboxes", ct), row => row.GetProperty("id").GetGuid() == inboxId)
            .GetProperty("_items").EnumerateArray().ToArray();

    [Fact]
    public async Task OrganisingCreatesTheTaskAndEmptiesTheItem()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands"),
            new CreateInbox(Guid.NewGuid(), user, inboxId),
            new CaptureToInbox(Guid.NewGuid(), user, inboxId, itemId, "call the dentist"));

        await SendAsync(client, ct, new OrganiseInboxItem(Guid.NewGuid(), user, inboxId, itemId, listId, taskId));

        var task = Assert.Single(await SyncedAsync(client, "todotasks", ct));
        Assert.Equal(taskId, task.GetProperty("id").GetGuid());
        Assert.Equal(listId, task.GetProperty("listId").GetGuid());
        Assert.Equal("call the dentist", task.GetProperty("name").GetString());
        Assert.Empty(await InboxItemsAsync(client, inboxId, ct));
    }

    [Fact]
    public async Task OrganisingIntoADeletedListIsRejectedAndTheItemStays()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands"),
            new DeleteTaskList(Guid.NewGuid(), user, listId),
            new CreateInbox(Guid.NewGuid(), user, inboxId),
            new CaptureToInbox(Guid.NewGuid(), user, inboxId, itemId, "call the dentist"));

        var result = Assert.Single(await TrySendAsync(client, ct,
            new OrganiseInboxItem(Guid.NewGuid(), user, inboxId, itemId, listId, Guid.NewGuid())));

        Assert.False(result.Accepted);
        Assert.Contains("list", result.Rejection, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await InboxItemsAsync(client, inboxId, ct));
        Assert.Empty(await SyncedAsync(client, "todotasks", ct));
    }
}
