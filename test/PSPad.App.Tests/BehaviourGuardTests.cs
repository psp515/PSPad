using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class BehaviourGuardTests
{
    [Fact]
    public void NoTestAssertsShape() =>
        BehaviourGuard.AssertNoShapeAssertions("PSPad.App.Tests");
}
