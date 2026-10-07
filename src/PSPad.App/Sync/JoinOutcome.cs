using System.Text.Json;

namespace PSPad.App.Sync;

public abstract record JoinOutcome
{
    public sealed record Joined(Guid ListId, IReadOnlyDictionary<string, JsonElement[]> Documents) : JoinOutcome;

    public sealed record Invalid : JoinOutcome;

    public sealed record Expired : JoinOutcome;

    public sealed record TooManyTries : JoinOutcome;
}
