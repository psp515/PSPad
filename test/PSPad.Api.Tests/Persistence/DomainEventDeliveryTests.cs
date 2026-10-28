using Microsoft.Extensions.DependencyInjection;
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
}
