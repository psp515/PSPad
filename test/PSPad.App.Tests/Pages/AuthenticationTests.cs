using System.Reflection;
using Microsoft.AspNetCore.Components;
using PSPad.App.Layout;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AuthenticationTests
{
    [Fact]
    public void TheLoginCallbackRouteNeverMountsAppShell()
    {
        // AppShell's account load is a one-shot check of the cascading auth state, run once
        // at OnInitializedAsync. On this route that check races RemoteAuthenticatorView's own
        // processing of the OIDC redirect and reliably sees "unauthenticated" first, locking
        // AppShell into an anonymous session (Guid.Empty) for the rest of the SPA's lifetime --
        // the client-side navigation RemoteAuthenticatorView does afterwards never remounts the
        // layout to re-check. Every command sent from then on embeds the wrong user id and the
        // server rejects it forever as "for a different user." Giving this route its own layout
        // keeps AppShell from ever seeing the mid-login state.
        var layout = typeof(Authentication).GetCustomAttribute<LayoutAttribute>();

        Assert.NotNull(layout);
        Assert.Equal(typeof(AuthenticationLayout), layout!.LayoutType);
        Assert.NotEqual(typeof(AppShell), layout.LayoutType);
    }
}
