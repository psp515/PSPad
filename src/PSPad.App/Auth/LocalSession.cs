namespace PSPad.App.Auth;

public sealed record LocalSession(
    Guid UserId,
    string DisplayName,
    string Email,
    string TimeZone,
    string Language,
    string RefreshToken,
    DateTimeOffset LastServerContactUtc,
    string? IdToken = null);
