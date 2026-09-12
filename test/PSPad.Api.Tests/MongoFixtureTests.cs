using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoFixtureTests(MongoFixture fixture)
{
    [Fact]
    public async Task TransactionsAreAvailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var collection = fixture.Database.GetCollection<BsonDocument>("transaction_probe");
        using var session = await fixture.Client.StartSessionAsync(cancellationToken: cancellationToken);

        session.StartTransaction();
        await collection.InsertOneAsync(session, new BsonDocument { { "_id", ObjectId.GenerateNewId() } }, cancellationToken: cancellationToken);
        await session.CommitTransactionAsync(cancellationToken);

        Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken));
    }
}
