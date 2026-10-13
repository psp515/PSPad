namespace PSPad.Api.Identity;

public sealed class AccountDeletionOptions
{
    public const string Section = "AccountDeletion";

    public TimeSpan MaxAuthenticationAge { get; init; } = TimeSpan.FromMinutes(5);
}
