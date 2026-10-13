using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Deployment;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoAppUserTests(MongoFixture fixture)
{
    const string User = "pspad-api";
    const string Script = "/tmp/mongo-app-user.js";

    [Fact]
    public async Task TheApiUserWritesItsOwnDatabaseAndNoOther()
    {
        var database = NewDatabase();
        await EnsureUserAsync(database, "first-secret");
        var client = ClientAs(database, "first-secret");

        await InsertAsync(client, database);
        var refused = await Assert.ThrowsAnyAsync<MongoException>(() => InsertAsync(client, NewDatabase()));

        Assert.Contains("not authorized", refused.Message);
    }

    [Fact]
    public async Task RunningItAgainTakesTheNewPassword()
    {
        var database = NewDatabase();
        await EnsureUserAsync(database, "first-secret");

        await EnsureUserAsync(database, "second-secret");

        await InsertAsync(ClientAs(database, "second-secret"), database);
        await Assert.ThrowsAsync<MongoAuthenticationException>(() => InsertAsync(ClientAs(database, "first-secret"), database));
    }

    async Task EnsureUserAsync(string database, string password)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Container.CopyAsync(File.ReadAllBytes(Path.Combine(RepositoryFiles.Root, "docker", "mongo-app-user.js")), Script, ct: cancellationToken);

        var result = await fixture.Container.ExecAsync(
            ["sh", "-c", $"MONGO_DATABASE={database} MONGO_APP_USER={User} MONGO_APP_PASSWORD={password} mongosh --quiet -u mongo -p mongo --authenticationDatabase admin {Script}"],
            cancellationToken);

        Assert.True(result.ExitCode == 0, result.Stderr + result.Stdout);
    }

    MongoClient ClientAs(string database, string password) =>
        new(new MongoUrlBuilder(fixture.ConnectionString)
        {
            Username = User,
            Password = password,
            AuthenticationSource = database,
            DatabaseName = database,
        }.ToMongoUrl());

    static Task InsertAsync(IMongoClient client, string database) =>
        client.GetDatabase(database).GetCollection<BsonDocument>("probe")
            .InsertOneAsync(new BsonDocument(), cancellationToken: TestContext.Current.CancellationToken);

    static string NewDatabase() => $"app_user_{Guid.NewGuid():N}";
}
