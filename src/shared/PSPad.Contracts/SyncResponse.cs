using System.Text.Json;

namespace PSPad.Contracts;

public sealed record SyncResponse(
    long Marker,
    IReadOnlyDictionary<string, JsonElement[]> Documents,
    Guid[]? MemberListIds = null,
    bool HasMore = false);
