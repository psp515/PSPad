using PSPad.Module.Identity.Provisioning;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Identity.Tests;

[UnitTest]
public class ProvisioningPlanTests
{
    [Fact]
    public void EnglishSeedsPersonalWorkHome()
    {
        Assert.Equal(["Personal", "Work", "Home"], ProvisioningPlan.SeedAreaNames("en"));
    }

    [Fact]
    public void PolishSeedsPrywatnePracaDom()
    {
        Assert.Equal(["Prywatne", "Praca", "Dom"], ProvisioningPlan.SeedAreaNames("pl"));
    }

    [Fact]
    public void AnUnknownLanguageSeedsEnglish()
    {
        Assert.Equal(["Personal", "Work", "Home"], ProvisioningPlan.SeedAreaNames("de"));
    }
}
