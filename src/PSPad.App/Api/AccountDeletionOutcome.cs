namespace PSPad.App.Api;

public abstract record AccountDeletionOutcome
{
    public sealed record Deleted(bool KeycloakRemoved) : AccountDeletionOutcome;

    public sealed record ReauthenticationRequired : AccountDeletionOutcome;

    public sealed record Failed : AccountDeletionOutcome;
}
