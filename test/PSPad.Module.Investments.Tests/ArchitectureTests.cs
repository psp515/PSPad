using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void InvestmentsCarriesNoInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(InvestmentsModuleMarker).Assembly,
            "MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure", "System.Security.Cryptography");
    }

    [Fact]
    public void InvestmentsKnowsNoOtherModule()
    {
        AssemblyReferenceGuard.AssertReferencesNone(typeof(InvestmentsModuleMarker).Assembly, "PSPad.Module.");
    }

    [Fact]
    public void MoneyValuesComeFromAbstractions()
    {
        Assert.Equal(typeof(PSPad.Abstractions.Aggregate).Assembly, typeof(PSPad.Module.Money.Money).Assembly);
        Assert.Equal(typeof(PSPad.Abstractions.Aggregate).Assembly, typeof(PSPad.Module.Money.Values.Currencies).Assembly);
    }
}
