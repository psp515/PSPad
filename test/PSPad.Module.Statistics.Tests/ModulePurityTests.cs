using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class ModulePurityTests
{
    [Fact]
    public void StatisticsNeverReachesInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(IEventLog).Assembly,
            "PSPad.Infrastructure",
            "MongoDB",
            "Microsoft.AspNetCore");
    }
}
