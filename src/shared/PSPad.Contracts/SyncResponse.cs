using System.Text.Json;

namespace PSPad.Contracts;

public sealed record SyncEvent(
    long Seq, string AggregateType, Guid AggregateId, string Type, string Payload, DateTimeOffset At);

public sealed record SyncResponse(
    long Marker,
    IReadOnlyDictionary<string, JsonElement[]> Documents,
    SyncEvent[] Events);
