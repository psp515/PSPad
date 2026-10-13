namespace PSPad.App.Auth;

public abstract record RefreshOutcome
{
    public sealed record Renewed(
        string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, string? IdToken = null)
        : RefreshOutcome;

    public sealed record Offline : RefreshOutcome;

    public sealed record Revoked : RefreshOutcome;
}
