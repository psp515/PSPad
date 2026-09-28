using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class KeycloakAccountConsoleTests
{
    [Fact]
    public void ItPointsAtTheRealmsAccountConsoleRoot()
    {
        var url = KeycloakAccountConsole.UrlFor("http://localhost:8080/realms/psplace");

        Assert.Equal("http://localhost:8080/realms/psplace/account/", url);
    }

    [Fact]
    public void ItSurvivesAnAuthorityWrittenWithATrailingSlash()
    {
        var url = KeycloakAccountConsole.UrlFor("http://localhost:8080/realms/psplace/");

        Assert.Equal("http://localhost:8080/realms/psplace/account/", url);
    }
}
