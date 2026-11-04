using PSPad.App.State;
using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;
using static PSPad.App.Tests.State.Investments.Investing;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class OperationDaysTests
{
    [Fact]
    public void AnOperationLateInTheUtcEveningLandsOnTheNextLocalDayInWarsaw()
    {
        var account = AnAccount("IKE", "PLN");
        var late = Deposit(account, 100m, new DateOnly(2026, 10, 14), at: new TimeOnly(23, 30));
        var earlier = Deposit(account, 50m, new DateOnly(2026, 10, 14), at: new TimeOnly(9, 0));

        var days = OperationDays.Of([earlier, late], LocalToday.Zone("Europe/Warsaw"));

        Assert.Equal([new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14)], days.Select(day => day.Day));
        Assert.Equal([late.Id], days[0].Operations.Select(operation => operation.Id));
    }

    [Fact]
    public void OperationsInADayRunNewestFirst()
    {
        var account = AnAccount("IKE", "PLN");
        var morning = Deposit(account, 100m, new DateOnly(2026, 10, 14), at: new TimeOnly(8, 0));
        var noon = Deposit(account, 50m, new DateOnly(2026, 10, 14), at: new TimeOnly(10, 0));

        var days = OperationDays.Of([morning, noon], TimeZoneInfo.Utc);

        Assert.Equal([noon.Id, morning.Id], days.Single().Operations.Select(operation => operation.Id));
    }
}
