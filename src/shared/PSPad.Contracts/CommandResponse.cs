namespace PSPad.Contracts;

public sealed record CommandResponse(Guid CommandId, bool Accepted, string? Rejection, bool Unrecoverable = false);
