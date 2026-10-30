using PSPad.TestInfrastructure;

namespace PSPad.Module.Sharing.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.Module.Sharing.Tests");

    [Fact]
    public void NoTestDrivesAHandlerThroughAFakeStore() =>
        BehaviourGuard.AssertNoFakeStores("PSPad.Module.Sharing.Tests");
}
