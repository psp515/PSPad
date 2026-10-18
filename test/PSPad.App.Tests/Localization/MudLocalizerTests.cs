using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
[Collection("Culture")]
public class MudLocalizerTests
{
    [Fact]
    public void ThePagerSpeaksPolishUnderThePolishCulture()
    {
        var localizer = new ServiceCollection().AddLogging().AddLocalization()
            .AddTransient<MudLocalizer, PSPadMudLocalizer>().BuildServiceProvider()
            .GetRequiredService<MudLocalizer>();
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal("Następna strona", localizer["MudTablePager_NextPage"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}
