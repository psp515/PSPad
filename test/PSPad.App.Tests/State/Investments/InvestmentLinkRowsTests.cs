using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkRowsTests
{
    static readonly DateOnly Day = new(2026, 10, 9);

    static LinkRow Row(string currency, decimal value, decimal? rate = null, bool ticked = true, bool loading = false)
    {
        var account = Given.AnAccount(currency: currency);
        return new LinkRow(account, null)
        {
            Ticked = ticked,
            Loading = loading,
            Value = new AccountValueOnDay(account.Id, value, currency, new Dictionary<string, DateOnly?>(), false, false),
            Rate = rate,
            RateDate = Day
        };
    }

    [Fact]
    public void OnlyTickedLoadedRowsWorthSomethingAreReady()
    {
        var ready = Row("PLN", 10m);

        var rows = new[] { ready, Row("PLN", 0m), Row("PLN", -3m), Row("PLN", 5m, ticked: false), Row("PLN", 5m, loading: true) };

        Assert.Same(ready, Assert.Single(InvestmentLinkRows.Ready(rows)));
    }

    [Fact]
    public void TheTotalIsInZlotyAtEachRowsRate()
    {
        var rows = new[] { Row("PLN", 100m), Row("USD", 10m, 3.5m) };

        Assert.Equal(135m, InvestmentLinkRows.Total(rows));
    }

    [Fact]
    public void AForeignRowWithoutARateAddsNothingAndBlocksConfirming()
    {
        var rows = new[] { Row("PLN", 100m), Row("USD", 10m) };

        Assert.Equal(100m, InvestmentLinkRows.Total(rows));
        Assert.False(InvestmentLinkRows.CanConfirm(rows, busy: false));
    }

    [Fact]
    public void ConfirmingNeedsSomethingReadyAndNothingBusy()
    {
        Assert.True(InvestmentLinkRows.CanConfirm([Row("PLN", 1m)], busy: false));
        Assert.False(InvestmentLinkRows.CanConfirm([Row("PLN", 1m)], busy: true));
        Assert.False(InvestmentLinkRows.CanConfirm([Row("PLN", 0m)], busy: false));
    }
}
