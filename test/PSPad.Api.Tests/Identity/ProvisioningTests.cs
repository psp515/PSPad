using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Identity;
using PSPad.Api.Tests.Persistence;
using PSPad.Infrastructure;
using PSPad.Infrastructure.Events;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Identity.Provisioning;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ProvisioningTests(MongoFixture fixture)
{
    [Fact]
    public async Task AFirstSignInCreatesTheUserAnInboxAndTheSeedAreas()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var context = Persistence.TestContext.For(fixture);
        var provisioner = ProvisionerFor(context);

        var user = await provisioner.EnsureAsync(subject, "Łukasz", "Europe/Warsaw", ct);

        var areas = await context.Collection<BsonDocument>("areas")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", new BsonBinaryData(user.Id, GuidRepresentation.Standard)))
            .ToListAsync(ct);
        var inboxes = await context.Collection<BsonDocument>("inboxes")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", new BsonBinaryData(user.Id, GuidRepresentation.Standard)))
            .ToListAsync(ct);

        Assert.Equal("Europe/Warsaw", user.TimeZone);
        Assert.Equal(ProvisioningPlan.SeedAreaNames.Count, areas.Count);
        Assert.Single(inboxes);
    }

    [Fact]
    public async Task ASecondSignInProvisionsNothingNew()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var context = Persistence.TestContext.For(fixture);
        var provisioner = ProvisionerFor(context);

        var first = await provisioner.EnsureAsync(subject, "Łukasz", "Europe/Warsaw", ct);
        var second = await provisioner.EnsureAsync(subject, "Łukasz", "Europe/Warsaw", ct);

        var areas = await context.Collection<BsonDocument>("areas")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", new BsonBinaryData(first.Id, GuidRepresentation.Standard)))
            .ToListAsync(ct);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ProvisioningPlan.SeedAreaNames.Count, areas.Count);
    }

    static UserProvisioner ProvisionerFor(MongoContext context) =>
        new(
            new MongoDocumentStore<User>(context),
            new MongoDocumentStore<Area>(context),
            new MongoDocumentStore<Inbox>(context),
            new MongoUnitOfWork(context, new NullDomainEventDispatcher()),
            new SystemClock());
}
