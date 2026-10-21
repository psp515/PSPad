using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace PSPad.TestInfrastructure;

public sealed class MongoFixture : IAsyncLifetime
{
    readonly MongoDbContainer _container = new MongoDbBuilder("mongo:9")
        .WithReplicaSet()
        .Build();

    IMongoClient _client = null!;

    public MongoDbContainer Container => _container;

    public string ConnectionString => _container.GetConnectionString();

    public IMongoClient Client => _client;

    public IMongoDatabase Database => _client.GetDatabase("pspad_test");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _client = new MongoClient(ConnectionString);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
