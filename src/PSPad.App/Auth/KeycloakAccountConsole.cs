namespace PSPad.App.Auth;

public static class KeycloakAccountConsole
{
    // The console root, not a sub-page: its routes have moved between Keycloak versions.
    public static string UrlFor(string authority) => $"{authority.TrimEnd('/')}/account/";
}
