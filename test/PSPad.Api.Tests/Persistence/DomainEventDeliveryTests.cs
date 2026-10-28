using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Identity;
using Microsoft.Extensions.Logging;
using PSPad.Abstractions;
using PSPad.Api.Tests.Logging;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using static PSPad.Api.Tests.Statistics.StatisticsFeed;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class DomainEventDeliveryTests(MongoFixture fixture)
{
    [Fact(Timeout = 60000)]
    public async Task AHandlerThatThrowsNeverStopsLaterEventsBeingProjected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var handler = new ThrowingDomainEventHandler("Poison");
        await using var factory = new ApiFactory(fixture, testServices: services =>
        {
            services.AddRecordedLogs();
            services.AddSingleton<IDomainEventHandler>(handler);
        });
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);

        await SendAsync(client, ct, new PSPad.Module.Tasks.Tasks.CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "Poison"));
        await handler.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await SendAsync(client, ct, new PSPad.Module.Tasks.Tasks.CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "After"));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.TaskName == "After"), ct);
        Assert.Contains(feed, record => record.TaskName == "Poison");
        var failure = Assert.Single(
            factory.Services.GetRequiredService<RecordedLogs>().Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(nameof(ThrowingDomainEventHandler), failure.Message);
    }

    [Fact(Timeout = 60000)]
    public async Task DeletingTheAccountAfterAHandlerThrewDoesNotWaitOutTheDrain()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var handler = new ThrowingDomainEventHandler("Poison");
        await using var factory = new ApiFactory(fixture, new FakeKeycloakAdminClient(succeeds: true), testServices: services =>
        {
            services.AddRecordedLogs();
            services.AddSingleton<IDomainEventHandler>(handler);
        });
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        await SendAsync(client, ct, new PSPad.Module.Tasks.Tasks.CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "Poison"));
        await handler.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);

        var response = await client.DeleteAsync("/api/account", ct);

        response.EnsureSuccessStatusCode();
        AssertNoDrainWarning(factory);
        await AssertNothingLeftAsync(user, ct);
    }

    [Fact(Timeout = 60000)]
    public async Task DeletingTheAccountWithNothingToProjectDoesNotWaitOutTheDrain()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(
            fixture, new FakeKeycloakAdminClient(succeeds: true), testServices: services => services.AddRecordedLogs());
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await SignInAsync(client, ct);

        var response = await client.DeleteAsync("/api/account", ct);

        response.EnsureSuccessStatusCode();
        AssertNoDrainWarning(factory);
    }

    static void AssertNoDrainWarning(ApiFactory factory) =>
        Assert.DoesNotContain(
            factory.Services.GetRequiredService<RecordedLogs>().Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("did not drain"));

    async Task AssertNothingLeftAsync(Guid user, CancellationToken ct)
    {
        var context = TestContext.For(fixture);
        var names = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct)).ToListAsync(ct);

        foreach (var name in names)
        {
            var remaining = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", user))
                .CountDocumentsAsync(ct);
            Assert.True(remaining == 0, $"Collection '{name}' still has {remaining} document(s) for the deleted user.");
        }
    }
}
