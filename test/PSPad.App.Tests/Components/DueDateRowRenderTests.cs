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

        var text = row.Find("button").TextContent;
        Assert.Contains("Due", text);
        Assert.Contains("No due date", text);
    }

    [Fact]
    public void APassedEmptyTextWinsOverTheDefault()
    {
        var row = Render<DueDateRow>(parameters => parameters
            .Add(p => p.Today, new DateOnly(2026, 9, 24))
            .Add(p => p.EmptyText, "No end date"));

        Assert.Contains("No end date", row.Find("button").TextContent);
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

            var text = row.Find("button").TextContent;
            Assert.Contains("Termin", text);
            Assert.Contains("Brak terminu", text);
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

            Assert.Contains("18 Oct", english.Find("button").TextContent);

            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var polish = Render<DueDateRow>(parameters => parameters
                .Add(p => p.Today, new DateOnly(2026, 10, 1))
                .Add(p => p.Value, new DateOnly(2026, 10, 18)));

            Assert.Contains("18 paź", polish.Find("button").TextContent);
            Assert.DoesNotContain("Oct", polish.Find("button").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
