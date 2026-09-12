using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoContext
{
    public MongoContext(IOptions<MongoOptions> options)
    {
        Client = new MongoClient(options.Value.ConnectionString);
        Database = Client.GetDatabase(options.Value.Database);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<T> Collection<T>() where T : Aggregate =>
        Database.GetCollection<T>(NameOf(typeof(T)));

    public IMongoCollection<TDocument> Collection<TDocument>(string name) =>
        Database.GetCollection<TDocument>(name);

    public static string NameOf(Type aggregate)
    {
        var name = aggregate.Name.ToLowerInvariant();
        return name.EndsWith('x') ? name + "es" : name + "s";
    }
}
