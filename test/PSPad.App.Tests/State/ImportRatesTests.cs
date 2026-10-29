using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.Contracts;
using PSPad.Module.Money.Import;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class ImportRatesTests
{
    static readonly IStringLocalizer<BudgetsStrings> Text = TestLocalizers.For<BudgetsStrings>();
    const string Offline = "Needs a rate: go online or fill the rate column.";

    static readonly RateRequest Eur = new("EUR", new DateOnly(2026, 9, 5));
    static readonly RateRequest Usd = new("USD", new DateOnly(2026, 9, 5));
    static readonly DateOnly Today = new(2026, 10, 14);

    [Fact]
    public async Task OfflineEveryRequestFailsWithoutAsking()
    {
        var nbp = new FakeNbpRates();

        var book = await ImportRates.FetchAsync(nbp, false, Today, [Eur], RateBook.Empty, Text);

        Assert.Equal(Offline, book.Failed[Eur]);
        Assert.Empty(nbp.Asked);
    }

    [Fact]
    public async Task RatesFailuresSpeakPolishWithPolishDates()
    {
        var before = System.Globalization.CultureInfo.CurrentUICulture;
        var beforeFormat = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
            var nbp = new FakeNbpRates();
            nbp.ByCurrency["USD"] = null;

            var book = await ImportRates.FetchAsync(nbp, true, Today, [Usd], RateBook.Empty, Text);

            Assert.Equal("Brak kursu NBP dla USD z dnia 5.09.2026.", book.Failed[Usd]);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = before;
            System.Globalization.CultureInfo.CurrentCulture = beforeFormat;
        }
    }

    [Fact]
    public async Task AFutureDateFailsWithoutAskingAndSaysTheRateIsNotPublishedYet()
    {
        var nbp = new FakeNbpRates();
        var future = new RateRequest("USD", new DateOnly(2026, 10, 31));

        var book = await ImportRates.FetchAsync(nbp, true, Today, [future], RateBook.Empty, Text);

        Assert.Equal("10/31/2026 is in the future, so NBP has no USD rate for it yet. Fill the rate column or change the date.",
            book.Failed[future]);
        Assert.Empty(nbp.Asked);
    }
}
