namespace PSPad.Abstractions;

public sealed class DomainRejectedException(string reason, bool Unrecoverable = false) : Exception(reason)
{
    public bool Unrecoverable { get; } = Unrecoverable;
}
