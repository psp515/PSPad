using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class KeycloakEndSessionTests
{
    [Fact]
    public void ItPointsAtTheRealmsEndSessionEndpoint()
    {
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace", "pspad-frontend", "http://localhost:5001/welcome");

        Assert.StartsWith(
            "http://localhost:8080/realms/psplace/protocol/openid-connect/logout?", url);
    }

    [Fact]
    public void ItIdentifiesTheClientSoKeycloakAcceptsThePostLogoutRedirectWithoutAnIdTokenHint()
    {
        // A session stored before id tokens were kept has no hint to send; client_id still lets Keycloak validate the redirect.
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace", "pspad-frontend", "http://localhost:5001/welcome");

        Assert.Contains("client_id=pspad-frontend", url);
    }

    [Fact]
    public void ItEscapesTheReturnAddress()
    {
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace", "pspad-frontend", "http://localhost:5001/welcome");

        Assert.Contains(
            "post_logout_redirect_uri=http%3A%2F%2Flocalhost%3A5001%2Fwelcome", url);
    }

    [Fact]
    public void ItSurvivesAnAuthorityWrittenWithATrailingSlash()
    {
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace/", "pspad-frontend", "http://localhost:5001/welcome");

        Assert.StartsWith(
            "http://localhost:8080/realms/psplace/protocol/openid-connect/logout?", url);
    }

    [Fact]
    public void ItHintsTheSessionToEndSoKeycloakSkipsItsConfirmationPage()
    {
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace", "pspad-frontend", "http://localhost:5001/welcome", "a.b+c/d");

        Assert.Contains("id_token_hint=a.b%2Bc%2Fd", url);
    }

    [Fact]
    public void ItLeavesTheHintOutWhenThereIsNoIdToken()
    {
        var url = KeycloakEndSession.UrlFor(
            "http://localhost:8080/realms/psplace", "pspad-frontend", "http://localhost:5001/welcome", null);

        Assert.DoesNotContain("id_token_hint", url);
    }
}
