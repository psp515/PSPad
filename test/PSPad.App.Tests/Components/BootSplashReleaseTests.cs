using Bunit;
using Microsoft.AspNetCore.Components;
using PSPad.App.Components;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class BootSplashReleaseTests : Bunit.TestContext
{
    [Fact]
    public void ItTearsTheBootSplashDownOnceItHasRendered()
    {
        JSInterop.SetupVoid("pspadBoot.done");

        Render<BootSplashRelease>();

        JSInterop.VerifyInvoke("pspadBoot.done");
    }

    [Fact]
    public void AFailedTeardownDoesNotBreakTheScreenBeneathIt()
    {
        JSInterop.SetupVoid("pspadBoot.done").SetException(new InvalidOperationException("gone"));

        var release = Render<BootSplashRelease>();

        Assert.Empty(release.Markup);
    }

    [Fact]
    public void TheSignedOutScreensTearTheSplashDown()
    {
        JSInterop.SetupVoid("pspadBoot.done");

        Render<PublicLayout>(parameters => parameters.Add(layout => layout.Body, Content));

        JSInterop.VerifyInvoke("pspadBoot.done");
    }

    [Fact]
    public void TheSignInCallbackScreensTearTheSplashDown()
    {
        JSInterop.SetupVoid("pspadBoot.done");

        Render<AuthenticationLayout>(parameters => parameters.Add(layout => layout.Body, Content));

        JSInterop.VerifyInvoke("pspadBoot.done");
    }

    static readonly RenderFragment Content = builder => builder.AddContent(0, "screen");
}
