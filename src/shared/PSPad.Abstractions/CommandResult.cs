namespace PSPad.Abstractions;

public sealed record CommandResult(bool Accepted, string? Rejection = null)
{
    public static CommandResult Ok() => new(true);

    public static CommandResult Rejected(string reason) => new(false, reason);
}
