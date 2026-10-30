using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class LeadTimeTextTests : IDisposable
{
    static readonly Microsoft.Extensions.Localization.IStringLocalizer<TasksStrings> Strings = TestLocalizers.For<TasksStrings>();

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void WithNoLeadTimeItReadsTheDefaultWeek() => Assert.Equal("1 week before", LeadTimeText.Describe(null, Strings));

    [Theory]
    [InlineData(1, LeadUnit.Days, "1 day before")]
    [InlineData(3, LeadUnit.Days, "3 days before")]
    [InlineData(1, LeadUnit.Weeks, "1 week before")]
    [InlineData(2, LeadUnit.Weeks, "2 weeks before")]
    [InlineData(1, LeadUnit.Months, "1 month before")]
    [InlineData(6, LeadUnit.Months, "6 months before")]
    public void ItReadsTheAmountAndUnit(int amount, LeadUnit unit, string expected) =>
        Assert.Equal(expected, LeadTimeText.Describe(new LeadTime(amount, unit), Strings));

    [Fact]
    public void TheUnitLabelAgreesWithTheAmountInPolish()
    {
        CultureScope.Use("pl-PL");

        Assert.Equal("miesięcy", LeadTimeText.UnitLabel(LeadUnit.Months, 6, Strings));
    }
}
