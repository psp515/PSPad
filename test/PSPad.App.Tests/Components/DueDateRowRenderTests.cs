using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class DueDateRowRenderTests : Bunit.TestContext
{
    public DueDateRowRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    [Fact]
    public void WithoutADateItSaysThereIsNoDueDate()
    {
        var row = Render<DueDateRow>(parameters => parameters.Add(p => p.Today, new DateOnly(2026, 9, 24)));

        Assert.Equal("Due", row.Find(".pspad-property-label").TextContent.Trim());
        Assert.Equal("No due date", row.Find(".pspad-property-value").TextContent.Trim());
    }

    [Fact]
    public void APassedEmptyTextWinsOverTheDefault()
    {
        var row = Render<DueDateRow>(parameters => parameters
            .Add(p => p.Today, new DateOnly(2026, 9, 24))
            .Add(p => p.EmptyText, "No end date"));

        Assert.Equal("No end date", row.Find(".pspad-property-value").TextContent.Trim());
    }

    [Fact]
    public void WithoutADateItSpeaksPolishUnderThePolishCulture()
    {
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            var row = Render<DueDateRow>(parameters => parameters.Add(p => p.Today, new DateOnly(2026, 9, 24)));

            Assert.Equal("Termin", row.Find(".pspad-property-label").TextContent.Trim());
            Assert.Equal("Brak terminu", row.Find(".pspad-property-value").TextContent.Trim());
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }

    [Fact]
    public void ADueDateRendersDayFirstInEnglishAndPolishMonthInPolish()
    {
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            var english = Render<DueDateRow>(parameters => parameters
                .Add(p => p.Today, new DateOnly(2026, 10, 1))
                .Add(p => p.Value, new DateOnly(2026, 10, 18)));

            Assert.Contains("18 Oct", english.Find(".pspad-property-value").TextContent);

            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var polish = Render<DueDateRow>(parameters => parameters
                .Add(p => p.Today, new DateOnly(2026, 10, 1))
                .Add(p => p.Value, new DateOnly(2026, 10, 18)));

            Assert.Contains("18 paź", polish.Find(".pspad-property-value").TextContent);
            Assert.DoesNotContain("Oct", polish.Find(".pspad-property-value").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
