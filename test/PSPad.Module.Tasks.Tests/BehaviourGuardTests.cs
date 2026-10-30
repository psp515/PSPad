using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.Module.Tasks.Tests");

    [Fact]
    public void NoTestDrivesAHandlerThroughAFakeStore() =>
        BehaviourGuard.AssertNoFakeStores("PSPad.Module.Tasks.Tests");
}
