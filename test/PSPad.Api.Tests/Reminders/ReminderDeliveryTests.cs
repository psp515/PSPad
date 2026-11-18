using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Api.Reminders;
using PSPad.Api.Tests.Identity;
using PSPad.Api.Tests.Logging;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Reminders;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Reminders;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ReminderDeliveryTests(MongoFixture fixture)
{
    const string Contact = "mailto:reminders@pspad.test";
    const string AppBase = "https://pspad.test";
    static readonly TimeOnly Nine = new(9, 0);
    static readonly DateTimeOffset SeededAt = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    static CancellationToken Ct => global::Xunit.TestContext.Current.CancellationToken;

    ApiFactory FactoryWith(FakePushService push, IKeycloakAdminClient? keycloak = null, string contact = Contact) =>
        new(fixture, keycloak,
            new Dictionary<string, string?>
            {
                ["Reminders:AppBaseAddress"] = AppBase,
                ["Reminders:Contact"] = contact,
            },
            services =>
            {
                services.AddRecordedLogs();
                services.AddHttpClient<IPushSender, WebPushSender>().ConfigurePrimaryHttpMessageHandler(() => push);
                services.AddSingleton<IClock>(new TestClock(SeededAt));
            });

    static DateTimeOffset At(DateOnly day, TimeOnly time) =>
        new(day.ToDateTime(time), TimeSpan.Zero);

    static async Task<TickResult> TickAsync(ApiFactory factory, DateTimeOffset from, DateTimeOffset now)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderTick>().RunAsync(from, now, Ct);
    }

    static async Task<Guid> ListAsync(HttpClient client, Guid userId)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Sharing.SendAsync(client, Ct,
            new CreateArea(Guid.NewGuid(), userId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), userId, listId, areaId, "Bills", 0));
        return listId;
    }

    static async Task<Guid> TaskAsync(
        HttpClient client, Guid userId, Guid listId, string name, DateOnly due, int daysBefore, TimeOnly at)
    {
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(client, Ct,
            new CreateTask(Guid.NewGuid(), userId, taskId, listId, name),
            new SetTaskDueDate(Guid.NewGuid(), userId, taskId, due),
            new SetTaskReminder(Guid.NewGuid(), userId, taskId, TaskReminder.Of(daysBefore, at)));
        return taskId;
    }

    static async Task<BrowserDevice> DeviceAsync(ApiFactory factory, Guid userId)
    {
        var device = BrowserDevice.Create();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>()
            .SaveAsync(device.RecordFor(userId, SeededAt), Ct);
        return device;
    }

    static async Task<(HttpClient Client, Guid UserId)> UserAsync(ApiFactory factory)
    {
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        return (client, await Sharing.SignInAsync(client, Ct));
    }

    IMongoCollection<BsonDocument> Collection(string name) =>
        Persistence.TestContext.For(fixture).Collection<BsonDocument>(name);

    [Fact]
    public async Task ATickAtTheMomentSendsOneEncryptedSignedPush()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 1);
        var taskId = await TaskAsync(client, userId, listId, "Pay rent", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        var request = Assert.Single(push.To(device));
        Assert.Equal(((int)TimeSpan.FromHours(15).TotalSeconds).ToString(), request.Headers["TTL"]);
        Assert.Equal(taskId.ToString("N"), request.Headers["Topic"]);
        Assert.Equal("normal", request.Headers["Urgency"]);
        Assert.Equal("aes128gcm", request.Headers["Content-Encoding"]);

        var stored = await Collection("vapidkeys").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync(Ct);
        var claims = Vapid.Verify(request.Headers["Authorization"], stored["publicKey"].AsString);
        Assert.Equal("https://push.test", claims.GetProperty("aud").GetString());
        Assert.Equal(Contact, claims.GetProperty("sub").GetString());
        Assert.True(claims.TryGetProperty("exp", out _));

        var payload = JsonDocument.Parse(device.Decrypt(request)).RootElement;
        Assert.Equal("Pay rent", payload.GetProperty("title").GetString());
        Assert.Equal("Due today", payload.GetProperty("body").GetString());
        Assert.EndsWith($"/?task={taskId}", payload.GetProperty("url").GetString());
        Assert.Equal(taskId.ToString(), payload.GetProperty("tag").GetString());
    }

    [Fact]
    public async Task TwoTicksOverOneWindowSendOnce()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 2);
        var sequential = await TaskAsync(client, userId, listId, "Water plants", day, 0, Nine);
        var parallel = await TaskAsync(client, userId, listId, "Feed cat", day, 0, new TimeOnly(10, 0));
        using var device = await DeviceAsync(factory, userId);

        var first = At(day, Nine);
        await TickAsync(factory, first.AddMinutes(-1), first);
        await TickAsync(factory, first.AddMinutes(-1), first);

        var second = At(day, new TimeOnly(10, 0));
        await Task.WhenAll(
            TickAsync(factory, second.AddMinutes(-1), second),
            TickAsync(factory, second.AddMinutes(-1), second));

        var requests = push.To(device);
        Assert.Single(requests, request => request.Headers["Topic"] == sequential.ToString("N"));
        Assert.Single(requests, request => request.Headers["Topic"] == parallel.ToString("N"));
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task AGoneSubscriptionIsDeleted()
    {
        var push = new FakePushService();
        push.Answer(HttpStatusCode.Gone);
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 3);
        await TaskAsync(client, userId, listId, "Call mum", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        Assert.Single(push.To(device));
        Assert.Equal(0, await Collection("pushsubscriptions")
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("endpoint", device.Endpoint), cancellationToken: Ct));
    }

    [Fact]
    public async Task AnUnavailableServiceIsRetriedAndGivenUp()
    {
        var push = new FakePushService();
        push.Answer(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable);
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 4);
        var taskId = await TaskAsync(client, userId, listId, "Renew passport", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);
        await TickAsync(factory, moment, moment.AddMinutes(1));
        await TickAsync(factory, moment.AddMinutes(1), moment.AddMinutes(2));

        Assert.Equal(3, push.To(device).Count);
        var claim = await Collection("sentreminders")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ReminderClaim.IdFor(taskId, day, userId)))
            .SingleAsync(Ct);
        Assert.Equal((int)ClaimState.Failed, claim["state"].AsInt32);
        Assert.Equal(3, claim["attempts"].AsInt32);
    }

    [Fact]
    public async Task AStartAfterDowntimeCatchesUpAnHourOnly()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 5);
        var recent = await TaskAsync(client, userId, listId, "Recent", day, 0, new TimeOnly(11, 30));
        await TaskAsync(client, userId, listId, "Long ago", day, 0, new TimeOnly(10, 0));
        using var device = await DeviceAsync(factory, userId);

        var now = At(day, new TimeOnly(12, 0));
        await TickAsync(factory, now - ReminderTick.CatchUp, now);

        var request = Assert.Single(push.To(device));
        Assert.Equal(recent.ToString("N"), request.Headers["Topic"]);
    }

    [Fact]
    public async Task ATaskDoneBeforeTheMomentSendsNothing()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 6);
        var taskId = await TaskAsync(client, userId, listId, "Already done", day, 0, Nine);
        await Sharing.SendAsync(client, Ct, new CompleteTask(Guid.NewGuid(), userId, taskId));
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        Assert.Empty(push.To(device));
    }

    [Fact]
    public async Task ASharedListRemindsOwnerAndMember()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (owner, ownerId) = await UserAsync(factory);
        var (member, memberId) = await UserAsync(factory);
        (await member.PutAsJsonAsync("/api/me/language", new SetLanguageRequest("pl"), Ct)).EnsureSuccessStatusCode();
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, Ct, token: token);
        (await Sharing.JoinAsync(member, Ct, token)).EnsureSuccessStatusCode();
        var day = new DateOnly(2031, 2, 7);
        await TaskAsync(owner, ownerId, listId, "Kupić mleko", day, 0, Nine);
        using var ownerDevice = await DeviceAsync(factory, ownerId);
        using var memberDevice = await DeviceAsync(factory, memberId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        var toOwner = Assert.Single(push.To(ownerDevice));
        var toMember = Assert.Single(push.To(memberDevice));
        Assert.Equal("Due today", JsonDocument.Parse(ownerDevice.Decrypt(toOwner)).RootElement.GetProperty("body").GetString());
        Assert.Equal("Termin dziś", JsonDocument.Parse(memberDevice.Decrypt(toMember)).RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task ATaskWhoseListIsGoneSendsNothing()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var missingList = await ListAsync(client, userId);
        var deletedList = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 8);
        await TaskAsync(client, userId, missingList, "Orphan", day, 0, Nine);
        await TaskAsync(client, userId, deletedList, "In a deleted list", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);
        var lists = Collection("tasklists");
        await lists.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", missingList), Ct);
        await lists.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", deletedList), Builders<BsonDocument>.Update.Set("deleted", true),
            cancellationToken: Ct);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        Assert.Empty(push.To(device));
    }

    [Fact]
    public async Task TheKeyPairIsMadeOnce()
    {
        await Collection("vapidkeys").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, Ct);
        await using var first = FactoryWith(new FakePushService());
        await using var second = FactoryWith(new FakePushService());
        var one = first.Services.GetRequiredService<VapidKeyring>();
        var other = second.Services.GetRequiredService<VapidKeyring>();

        var keys = await Task.WhenAll(one.GetAsync(Ct), other.GetAsync(Ct), one.GetAsync(Ct));

        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(keys[0], keys[2]);
        Assert.Equal(1, await Collection("vapidkeys").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: Ct));
    }

    [Fact]
    public async Task AccountDeletionErasesSubscriptionsAndClaims()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push, new FakeKeycloakAdminClient(succeeds: true));
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 9);
        await TaskAsync(client, userId, listId, "Last thing", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);
        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);
        var byUser = Builders<BsonDocument>.Filter.Eq("userId", userId);
        Assert.Equal(1, await Collection("sentreminders").CountDocumentsAsync(byUser, cancellationToken: Ct));
        client.DefaultRequestHeaders.Add("X-Test-Auth-Time", SeededAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        (await client.DeleteAsync("/api/account", Ct)).EnsureSuccessStatusCode();

        Assert.Equal(0, await Collection("pushsubscriptions").CountDocumentsAsync(byUser, cancellationToken: Ct));
        Assert.Equal(0, await Collection("sentreminders").CountDocumentsAsync(byUser, cancellationToken: Ct));
    }

    [Fact]
    public async Task AContactWithoutASchemeIsSentAsMailto()
    {
        var push = new FakePushService();
        await using var factory = FactoryWith(push, contact: "you@example.com");
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 10);
        await TaskAsync(client, userId, listId, "Book dentist", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        var request = Assert.Single(push.To(device));
        var keys = await factory.Services.GetRequiredService<VapidKeyring>().GetAsync(Ct);
        var claims = Vapid.Verify(request.Headers["Authorization"], keys.PublicKey);
        Assert.Equal("mailto:you@example.com", claims.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task ARefusedPushIsLoggedWithItsStatusOnly()
    {
        var push = new FakePushService();
        push.Answer(HttpStatusCode.Forbidden);
        await using var factory = FactoryWith(push);
        var (client, userId) = await UserAsync(factory);
        var listId = await ListAsync(client, userId);
        var day = new DateOnly(2031, 2, 11);
        await TaskAsync(client, userId, listId, "Secret errand", day, 0, Nine);
        using var device = await DeviceAsync(factory, userId);

        var moment = At(day, Nine);
        await TickAsync(factory, moment.AddMinutes(-1), moment);

        var refused = Assert.Single(
            factory.Services.GetRequiredService<RecordedLogs>().Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message.StartsWith("Push service refused a reminder"));
        Assert.Contains("403", refused.Message);
        Assert.DoesNotContain(device.Endpoint, refused.Message);
        Assert.DoesNotContain("Secret errand", refused.Message);
    }

    [Fact]
    public async Task TheReminderIndexCoversOnlySetReminders()
    {
        await using var factory = FactoryWith(new FakePushService());
        _ = factory.Services;

        var indexes = await (await Collection("todotasks").Indexes.ListAsync(Ct)).ToListAsync(Ct);

        var index = Assert.Single(indexes, candidate => candidate["name"] == "reminder_set");
        Assert.False(index.Contains("sparse"));
        Assert.Equal(
            new BsonDocument("reminder", new BsonDocument("$type", (int)BsonType.Document)),
            index["partialFilterExpression"].AsBsonDocument);
    }
}
