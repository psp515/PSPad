using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class MoneyHeroTests : Bunit.TestContext
{
    public MoneyHeroTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void ItsTopRowIsLaidOutByItsOwnStylesNotByImportantUtilityClasses()
    {
        var hero = Render<MoneyHero>(parameters => parameters.Add(p => p.Label, "Net").Add(p => p.Text, "0.00 PLN"));

        var top = hero.Find(".pspad-money-hero-top");

        Assert.DoesNotContain("flex-column", top.ClassList);
        Assert.DoesNotContain("d-flex", top.ClassList);
    }
}
