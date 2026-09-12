using System.Text.Json;

namespace PSPad.Contracts;

public sealed record CommandEnvelope(string Type, JsonElement Payload);
