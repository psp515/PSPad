using PSPad.App.State;
using PSPad.Module.Money.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class InsightTextTests
{
    [Fact]
    public void PastUsualNamesTheCategoryAndTheAverage()
    {
        var insight = new Insight(InsightKind.PastUsual, "Food", 700m, 620m, 0);

        Assert.Equal("Food already past a usual month", InsightText.Title(insight));
        Assert.Equal("700.00 PLN vs 620.00 PLN average", InsightText.Caption(insight));
    }

    [Fact]
    public void RunningHighSaysHowMuchMoreThanUsual()
    {
        var insight = new Insight(InsightKind.RunningHigh, "Food", 300m, 200m, 0);

        Assert.Equal("Food is running high", InsightText.Title(insight));
        Assert.Equal("300.00 PLN so far, 100.00 PLN more than usual at this point", InsightText.Caption(insight));
    }

    [Fact]
    public void IncomeExpectedNamesTheDay()
    {
        var insight = new Insight(InsightKind.IncomeExpected, "Salary", 3000m, 0m, 28);

        Assert.Equal("Salary usually lands around the 28th", InsightText.Title(insight));
        Assert.Equal("3,000.00 PLN on average; nothing yet this month", InsightText.Caption(insight));
        Assert.Equal("Bonus usually lands around the 1st", InsightText.Title(insight with { Category = "Bonus", Day = 1 }));
        Assert.Equal("Bonus usually lands around the 22nd", InsightText.Title(insight with { Category = "Bonus", Day = 22 }));
        Assert.Equal("Bonus usually lands around the 3rd", InsightText.Title(insight with { Category = "Bonus", Day = 3 }));
        Assert.Equal("Bonus usually lands around the 11th", InsightText.Title(insight with { Category = "Bonus", Day = 11 }));
    }

    [Fact]
    public void OnTrackIsShort()
    {
        var insight = new Insight(InsightKind.OnTrack, "Subscriptions", 56.86m, 56m, 0);

        Assert.Equal("Subscriptions on track", InsightText.Title(insight));
        Assert.Equal("56.86 PLN, close to usual", InsightText.Caption(insight));
    }
}
