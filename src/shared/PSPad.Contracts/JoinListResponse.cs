using System.Text.Json;

namespace PSPad.Contracts;

public sealed record JoinListResponse(Guid ListId, IReadOnlyDictionary<string, JsonElement[]> Documents);
