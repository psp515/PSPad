using PSPad.TestInfrastructure;

namespace PSPad.Module.Sharing.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void SharingCarriesNoInfrastructure() =>
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(SharingModuleMarker).Assembly,
            "MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure");

    [Fact]
    public void SharingKnowsOnlyTasks() =>
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(SharingModuleMarker).Assembly,
            "PSPad.Module.Statistics", "PSPad.Module.Presentation", "PSPad.Module.Identity");
}
