using System.Globalization;
using Bunit;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
[Collection("Culture")]
public class TodayLocalizationTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void TheEmptyDaySpeaksPolishUnderThePolishCulture()
    {
        AppTestHost.Arrange(this, User, Today);
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            var page = Render<Today>();

            Assert.Contains("Na dziś nic nie zaplanowano.", page.Markup);
            Assert.DoesNotContain("Nothing planned", page.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
