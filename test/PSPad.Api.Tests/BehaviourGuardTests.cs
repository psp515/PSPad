using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.Api.Tests");

    [Fact]
    public void NoTestDrivesAHandlerThroughAFakeStore() =>
        BehaviourGuard.AssertNoFakeStores("PSPad.Api.Tests");
}
