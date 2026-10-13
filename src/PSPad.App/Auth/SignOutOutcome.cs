namespace PSPad.App.Auth;

public abstract record SignOutOutcome
{
    public sealed record Cleared(string? IdTokenHint) : SignOutOutcome;

    public sealed record KeycloakUnreachable : SignOutOutcome;
}
