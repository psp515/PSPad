using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class DueDayTextTests : IDisposable
{
    static readonly DateOnly Thursday = new(2026, 9, 24);

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Tomorrow")]
    [InlineData(-1, "Yesterday")]
    [InlineData(5, "Tue, 29 Sept")]
    [InlineData(120, "Fri, 22 Jan 2027")]
    public void ItDescribesTheDueDayRelativeToToday(int offset, string expected) =>
        Assert.Equal(expected, DueDayText.Describe(Thursday.AddDays(offset), Thursday, TestLocalizers.For<SharedStrings>()));

    [Fact]
    public void InPolishTheDayAndMonthArePolish()
    {
        CultureScope.Use("pl-PL");

        var text = DueDayText.Describe(new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 1), TestLocalizers.For<SharedStrings>());

        Assert.Contains("18 paź", text);
        Assert.DoesNotContain("Oct", text);
        Assert.Equal("Jutro", DueDayText.Describe(Thursday.AddDays(1), Thursday, TestLocalizers.For<SharedStrings>()));
    }
}
