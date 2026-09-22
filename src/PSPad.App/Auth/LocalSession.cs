namespace PSPad.App.Auth;

public sealed record LocalSession(
    Guid UserId,
    string DisplayName,
    string Email,
    string TimeZone,
    string RefreshToken,
    DateTimeOffset LastServerContactUtc);
