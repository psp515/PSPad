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

    public static string NameOf(Type aggregate) => Pluralise(aggregate.Name.ToLowerInvariant());

    public static string Pluralise(string name) => name switch
    {
        _ when name.EndsWith('s') => name,
        _ when name.EndsWith('x') => name + "es",
        _ when name.EndsWith('y') && name.Length > 1 && !"aeiou".Contains(name[^2]) => name[..^1] + "ies",
        _ => name + "s"
    };
}
