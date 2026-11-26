using PSPad.App.Push;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class PushStateTests
{
    [Theory]
    [InlineData(true, true, "default", null, PushCardState.NeedsHomeScreen)]
    [InlineData(false, true, "default", null, PushCardState.NeedsHomeScreen)]
    [InlineData(false, false, "default", null, PushCardState.Unsupported)]
    [InlineData(true, false, "denied", null, PushCardState.Denied)]
    [InlineData(true, false, "denied", "https://push.example/x", PushCardState.Denied)]
    [InlineData(true, false, "granted", "https://push.example/x", PushCardState.On)]
    [InlineData(true, false, "granted", null, PushCardState.Off)]
    [InlineData(true, false, "default", null, PushCardState.Off)]
    [InlineData(true, false, "default", "https://push.example/x", PushCardState.Off)]
    public void Maps_support_to_card_state(bool supported, bool needsHomeScreen, string permission, string? endpoint, PushCardState expected)
    {
        var support = new PushSupport(supported, needsHomeScreen, permission, endpoint, "Browser");

        Assert.Equal(expected, PushState.Of(support));
    }
}
