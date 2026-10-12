using PSPad.Infrastructure.Mongo;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class DatasetIdentityTests(MongoFixture fixture)
{
    [Fact]
    public async Task TheSameIdComesBackOnEveryRead()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var identity = new DatasetIdentity(Persistence.TestContext.For(fixture));

        var first = await identity.CurrentAsync(ct);
        var second = await identity.CurrentAsync(ct);

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ConcurrentFirstReadsAgreeOnOneId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        await new DatasetIdentity(context).ForgetAsync(ct);

        var reads = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => new DatasetIdentity(context).CurrentAsync(ct)));

        Assert.Single(reads.Distinct());
    }

    [Fact]
    public async Task DeletingTheDocumentMintsANewId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = Persistence.TestContext.For(fixture);
        var identity = new DatasetIdentity(context);
        var before = await identity.CurrentAsync(ct);

        await identity.ForgetAsync(ct);

        Assert.NotEqual(before, await identity.CurrentAsync(ct));
    }
}
