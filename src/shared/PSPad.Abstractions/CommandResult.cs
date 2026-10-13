namespace PSPad.Abstractions;

public sealed record CommandResult(bool Accepted, string? Rejection = null, bool Unrecoverable = false)
{
    public static CommandResult Ok() => new(true);

    public static CommandResult Rejected(string reason) => new(false, reason);

    public static CommandResult Rejected(DomainRejectedException rejection) =>
        new(false, rejection.Message, rejection.Unrecoverable);
}
