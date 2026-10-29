using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class DroppedTextTests
{
    [Fact]
    public void AUriListSkipsCommentLinesAndTakesTheFirstUri() =>
        Assert.Equal("https://example.com/first",
            DroppedText.Read("# a comment\r\n\r\n  https://example.com/first\nhttps://example.com/second", null));

    [Fact]
    public void WithoutAUriPlainTextIsTakenTrimmed() =>
        Assert.Equal("OneDrive/recipes.txt", DroppedText.Read("# only a comment", "  OneDrive/recipes.txt "));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "   ")]
    public void NothingUsableReadsAsNothing(string? uriList, string? text) => Assert.Null(DroppedText.Read(uriList, text));
}
