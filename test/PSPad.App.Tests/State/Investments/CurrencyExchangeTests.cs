using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.App.Tests.Localization;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.App.Tests.State.Investments.Investing;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class CurrencyExchangeTests
{
    static readonly DateOnly Today = new(2026, 10, 30);
    static readonly DateOnly First = new(2026, 9, 1);

    readonly InMemoryReplica _replica = new();
    readonly StubInvestmentQuotes _quotes = new();
    readonly InvestmentAccount _pln = AnAccount("Main", "PLN");
    readonly InvestmentAccount _other = AnAccount("Second", "PLN");
    readonly InvestmentAccount _usd = AnAccount("Dollars", "USD");

    async Task<InvestmentOverview> OverviewAsync(bool converted = true)
    {
        await _replica.SaveAsync(_pln);
        await _replica.SaveAsync(_other);
        await _replica.SaveAsync(_usd);
        await _replica.SaveAsync(Deposit(_pln, 20_000m, First));
        if (converted)
        {
            await _replica.SaveAsync(Conversion(_pln, OperationKind.TransferOut, 9_800m,
                new MoneyValue(2_500m, "USD", 3.92m, First), First.AddDays(1)));
            await _replica.SaveAsync(Conversion(_other, OperationKind.TransferOut, 430m,
                new MoneyValue(100m, "EUR", 4.3m, First), First.AddDays(2)));
        }

        _quotes.Rates["USD"] = [new DailyRate(First, 3.65m)];
        _quotes.Rates["EUR"] = [new DailyRate(First, 4.3m)];
        return await new InvestmentQuery(_replica, _quotes)
            .LoadAsync(Owner, Today, TimeZoneInfo.Utc, true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AllAccountsShowEveryConvertedCurrency()
    {
        var overview = await OverviewAsync();

        Assert.Equal(["EUR", "USD"], overview.ExchangesOf(null).Select(exchange => exchange.Currency));
    }

    [Fact]
    public async Task APlnAccountShowsOnlyItsOwnCurrenciesAndAForeignAccountNone()
    {
        var overview = await OverviewAsync();

        Assert.Equal(["USD"], overview.ExchangesOf(_pln.Id).Select(exchange => exchange.Currency));
        Assert.Empty(overview.ExchangesOf(_usd.Id));
    }

    [Fact]
    public async Task WithoutConversionsThereIsNoCard()
    {
        var overview = await OverviewAsync(converted: false);

        Assert.Empty(overview.ExchangesOf(null));
    }

    [Fact]
    public void ARowReadsBoughtSoldAndHeld()
    {
        using var culture = new CultureScope("en-US");
        var exchange = new CurrencyExchange("USD", 2_500m, 2_500m, 9_800m, 100m, 100m, 400m, 2_400m, 9_408m, 3.65m, 3,
            First, First.AddDays(8));

        var text = CurrencyExchangeText.For(exchange, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("bought 2,500.00 for 9,800.00 PLN · avg 3.9200", text.Bought);
        Assert.Equal("sold back 100.00 for 400.00 PLN · avg 4.0000", text.Sold);
        Assert.Equal("left from conversions 2,400.00 · NBP today 3.6500", text.Held);
        Assert.Equal("−648.00 PLN", text.Result);
        Assert.Equal("3 conversions · 1 Sep 2026 – 9 Sep 2026", text.Span);
    }

    [Fact]
    public void UnitsFromAnotherForeignCurrencyStayOutOfThePlnSentence()
    {
        using var culture = new CultureScope("en-US");
        var exchange = new CurrencyExchange("USD", 1_110m, 1_000m, 3_900m, 0m, 0m, 0m, null, null, 3.65m, 2, First, First);

        var text = CurrencyExchangeText.For(exchange, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("bought 1,000.00 for 3,900.00 PLN · avg 3.9000 · +110.00 from another currency", text.Bought);
    }

    [Fact]
    public void OnlyForeignToForeignUnitsShowWithoutPln()
    {
        using var culture = new CultureScope("en-US");
        var exchange = new CurrencyExchange("USD", 110m, 0m, 0m, 20m, 0m, 0m, null, null, 3.65m, 2, First, First);

        var text = CurrencyExchangeText.For(exchange, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("+110.00 from another currency", text.Bought);
        Assert.Equal("20.00 to another currency", text.Sold);
    }

    [Fact]
    public void ARowInPolishSaysWhatItBought()
    {
        using var culture = new CultureScope("pl-PL");
        var exchange = new CurrencyExchange("USD", 1_000m, 1_000m, 3_900m, 0m, 0m, 0m, null, null, 3.65m, 1, First, First);

        var text = CurrencyExchangeText.For(exchange, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("kupiono 1\u00a0000,00 za 3\u00a0900,00 PLN · śr. 3,9000", text.Bought);
        Assert.Null(text.Sold);
        Assert.Null(text.Held);
        Assert.Null(text.Result);
    }
}
