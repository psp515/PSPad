using PSPad.App.Components;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class LeadTimeRowTests
{
    [Fact]
    public void WithNoLeadTimeItReadsTheDefaultWeek()
    {
        Assert.Equal("1 week before", LeadTimeRow.Describe(null));
    }

    [Theory]
    [InlineData(1, LeadUnit.Days, "1 day before")]
    [InlineData(3, LeadUnit.Days, "3 days before")]
    [InlineData(1, LeadUnit.Weeks, "1 week before")]
    [InlineData(2, LeadUnit.Weeks, "2 weeks before")]
    [InlineData(1, LeadUnit.Months, "1 month before")]
    [InlineData(6, LeadUnit.Months, "6 months before")]
    public void ItReadsTheAmountAndUnit(int amount, LeadUnit unit, string expected)
    {
        Assert.Equal(expected, LeadTimeRow.Describe(new LeadTime(amount, unit)));
    }
}
