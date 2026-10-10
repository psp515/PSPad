using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void MoneyCarriesNoInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(MoneyModuleMarker).Assembly,
            "MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure", "System.Security.Cryptography");
    }

    [Fact]
    public void MoneyKnowsNoOtherModule()
    {
        AssemblyReferenceGuard.AssertReferencesNone(typeof(MoneyModuleMarker).Assembly, "PSPad.Module.");
    }
}
