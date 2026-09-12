namespace PSPad.Contracts;

public sealed record MeResponse(Guid UserId, string DisplayName, string TimeZone);
