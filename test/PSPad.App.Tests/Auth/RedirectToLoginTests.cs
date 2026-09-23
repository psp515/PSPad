using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class RedirectToLoginTests : Bunit.TestContext
{
    [Fact]
    public void ItSendsAnAnonymousVisitorToTheWelcomeScreenRatherThanStraightToKeycloak()
    {
        // Bouncing to the identity provider on sight means a signed-out visitor never sees the
        // app at all -- and lands straight back in it, because Keycloak's own session outlives ours.
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("inbox");

        Render<RedirectToLogin>();

        Assert.StartsWith("http://localhost/welcome?returnUrl=", navigation.Uri);
    }

    [Fact]
    public void ItRemembersWhereTheVisitorWasHeaded()
    {
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("inbox");

        Render<RedirectToLogin>();

        Assert.Equal("http://localhost/inbox", ReturnUrlQuery.From(navigation.Uri));
    }
}
