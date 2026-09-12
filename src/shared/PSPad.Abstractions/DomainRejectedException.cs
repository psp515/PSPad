namespace PSPad.Abstractions;

public sealed class DomainRejectedException(string reason) : Exception(reason);
