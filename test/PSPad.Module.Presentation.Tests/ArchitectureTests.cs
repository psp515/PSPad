using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void PresentationCarriesNoInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(PresentationModuleMarker).Assembly,
            "MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure");
    }

    [Fact]
    public void PresentationKnowsNoOtherModule()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(PresentationModuleMarker).Assembly, "PSPad.Module.");
    }
}
