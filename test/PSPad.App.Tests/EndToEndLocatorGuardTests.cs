using PSPad.TestInfrastructure;

namespace PSPad.App.Tests;

[UnitTest]
public class EndToEndLocatorGuardTests
{
    [Fact]
    public void EndToEndStepsLocateByRoleLabelOrText() =>
        EndToEndLocatorGuard.AssertLocatorsAreAccessible();
}
