namespace PSPad.Api.Identity;

public sealed class KeycloakAdminOptions
{
    public const string Section = "Keycloak";

    public string Authority { get; init; } = "";

    public string AdminUser { get; init; } = "";

    public string AdminPassword { get; init; } = "";
}
