namespace PSPad.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public const string Section = "Mongo";

    public string ConnectionString { get; init; } = "";

    public string Database { get; init; } = "pspad";
}
