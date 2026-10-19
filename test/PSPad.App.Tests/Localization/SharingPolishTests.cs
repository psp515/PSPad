using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Components;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.Statistics;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
[Collection("Culture")]
public class SharingPolishTests : Bunit.TestContext
{
    [Theory]
    [InlineData(0, "Nikt nie dołączy ze starym linkiem i kodem.")]
    [InlineData(1, "Nikt nie dołączy ze starym linkiem i kodem. 1 osoba, która już jest na liście, zostaje.")]
    [InlineData(3, "Nikt nie dołączy ze starym linkiem i kodem. 3 osoby, które już są na liście, zostają.")]
    [InlineData(5, "Nikt nie dołączy ze starym linkiem i kodem. 5 osób, które już są na liście, zostaje.")]
    public void TheOldLinkSentenceAgreesWithTheNumberOfPeopleWhoStay(int people, string expected)
    {
        var strings = TestLocalizers.For<SharingStrings>();
        var before = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            Assert.Equal(expected, strings.Plural(people, "OldLinkEndedList"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Theory]
    [InlineData(SharingFailure.Offline, "Zmiany w udostępnianiu wymagają połączenia. Spróbuj ponownie, gdy będziesz online.")]
    [InlineData(SharingFailure.ListGone, "Ta lista nie jest ci już udostępniana.")]
    public void ASharingFailureSpeaksPolish(SharingFailure failure, string expected)
    {
        var strings = TestLocalizers.For<SharingStrings>();
        var before = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            Assert.Equal(expected, strings.Of(new SharingRejection(failure)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void AServerReasonIsShownAsItCame() =>
        Assert.Equal("This budget is archived.",
            TestLocalizers.For<SharingStrings>().Of(new SharingRejection(SharingFailure.Server, "This budget is archived.")));

    [Fact]
    public void TheHeatmapSpeaksPolishUnderThePolishCulture()
    {
        Services.AddLocalization();
        var before = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            var heatmap = Render<ConsistencyHeatmap>(parameters => parameters
                .Add(component => component.Days, [new DailyCountView(new DateOnly(2026, 9, 1), 2)])
                .Add(component => component.Today, new DateOnly(2026, 9, 1)));

            Assert.Contains("Dotknij kwadratu", heatmap.Markup);
            Assert.Contains("Mniej", heatmap.Markup);
            Assert.Equal("2026-09-01: zrobiono 2", heatmap.Find(".pspad-heatmap-cell").GetAttribute("aria-label"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}
