using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TimeTextTests
{
    [Theory]
    [InlineData("14", 14, 0)]
    [InlineData("9", 9, 0)]
    [InlineData("21", 21, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("9:30", 9, 30)]
    [InlineData("1430", 14, 30)]
    [InlineData("14.30", 14, 30)]
    [InlineData("14,30", 14, 30)]
    [InlineData(" 7:05 ", 7, 5)]
    [InlineData("7:00", 7, 0)]
    [InlineData("700", 7, 0)]
    [InlineData("7.00", 7, 0)]
    public void TypedTextIsReadLeniently(string typed, int hour, int minute)
    {
        Assert.True(TimeText.TryParse(typed, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    [InlineData("nine")]
    [InlineData("12:3")]
    public void UnreadableTextIsRejected(string typed) => Assert.False(TimeText.TryParse(typed, out _));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void EmptyTextReadsAsNoTime(string? typed)
    {
        Assert.True(TimeText.TryParse(typed, out var time));
        Assert.Null(time);
    }
}
