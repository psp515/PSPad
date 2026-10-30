using PSPad.TestInfrastructure;

namespace PSPad.Module.Identity.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.Module.Identity.Tests");

    [Fact]
    public void NoTestDrivesAHandlerThroughAFakeStore() =>
        BehaviourGuard.AssertNoFakeStores("PSPad.Module.Identity.Tests");
}
