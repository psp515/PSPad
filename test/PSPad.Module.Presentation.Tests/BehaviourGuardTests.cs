using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.Module.Presentation.Tests");

    [Fact]
    public void NoTestDrivesAHandlerThroughAFakeStore() =>
        BehaviourGuard.AssertNoFakeStores("PSPad.Module.Presentation.Tests");
}
