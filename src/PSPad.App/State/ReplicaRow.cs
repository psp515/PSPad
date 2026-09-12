using System.Text.Json;
using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed record ReplicaRow(Guid Id, string Type, Guid UserId, string Json)
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        IncludeFields = true
    };

    public static ReplicaRow From(Aggregate aggregate) =>
        new(aggregate.Id, aggregate.GetType().Name, aggregate.UserId,
            JsonSerializer.Serialize(aggregate, aggregate.GetType(), Options));

    public static ReplicaRow FromServer(string type, Guid userId, Guid id, JsonElement document) =>
        new(id, type, userId, document.GetRawText());

    public T To<T>() where T : Aggregate =>
        JsonSerializer.Deserialize<T>(Json, Options)
        ?? throw new InvalidOperationException($"Could not restore a {Type} from the replica.");
}
