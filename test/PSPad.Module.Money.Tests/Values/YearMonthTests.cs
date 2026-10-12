using System.Text.Json;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Values;

[UnitTest]
public class YearMonthTests
{
    [Fact]
    public void AMonthIsTakenFromADate() =>
        Assert.Equal(new YearMonth(2026, 10), YearMonth.Of(new DateOnly(2026, 10, 31)));

    [Fact]
    public void ItKnowsItsFirstAndLastDay()
    {
        Assert.Equal(new DateOnly(2028, 2, 1), new YearMonth(2028, 2).FirstDay);
        Assert.Equal(new DateOnly(2028, 2, 29), new YearMonth(2028, 2).LastDay);
        Assert.Equal(new DateOnly(2026, 12, 31), new YearMonth(2026, 12).LastDay);
    }

    [Fact]
    public void NextAndPreviousCrossTheYear()
    {
        Assert.Equal(new YearMonth(2027, 1), new YearMonth(2026, 12).Next());
        Assert.Equal(new YearMonth(2025, 12), new YearMonth(2026, 1).Previous());
    }

    [Fact]
    public void ItContainsOnlyItsOwnDays()
    {
        var october = new YearMonth(2026, 10);

        Assert.True(october.Contains(new DateOnly(2026, 10, 1)));
        Assert.True(october.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(october.Contains(new DateOnly(2026, 9, 30)));
        Assert.False(october.Contains(new DateOnly(2025, 10, 15)));
    }

    [Fact]
    public void MonthsAreOrdered()
    {
        Assert.True(new YearMonth(2026, 9) < new YearMonth(2026, 10));
        Assert.True(new YearMonth(2027, 1) > new YearMonth(2026, 12));
        Assert.True(new YearMonth(2026, 10) <= new YearMonth(2026, 10));
        Assert.Equal(
            [new YearMonth(2025, 12), new YearMonth(2026, 1)],
            new[] { new YearMonth(2026, 1), new YearMonth(2025, 12) }.Order());
    }

    [Fact]
    public void ItPrintsAsYearDashMonth() => Assert.Equal("2026-03", new YearMonth(2026, 3).ToString());

    [Fact]
    public void ItParsesItsOwnText()
    {
        Assert.Equal(new YearMonth(2026, 10), YearMonth.Parse("2026-10"));
        Assert.Equal(new YearMonth(2027, 1), YearMonth.Parse(new YearMonth(2027, 1).ToString()));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-1")]
    [InlineData("26-10")]
    [InlineData("2026/10")]
    [InlineData("0000-10")]
    [InlineData("+026-10")]
    [InlineData("")]
    public void ItRejectsAnythingElse(string text) =>
        Assert.Throws<FormatException>(() => YearMonth.Parse(text));

    [Fact]
    public void JsonCarriesTheMonthAsText()
    {
        var json = JsonSerializer.Serialize(new YearMonth(2026, 3));

        Assert.Equal("\"2026-03\"", json);
        Assert.Equal(new YearMonth(2026, 3), JsonSerializer.Deserialize<YearMonth>(json));
    }

    [Fact]
    public void JsonRejectsAnInvalidMonth()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<YearMonth>("\"2026-13\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<YearMonth>("{\"Year\":2026,\"Month\":3}"));
    }
}
