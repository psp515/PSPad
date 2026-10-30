using PSPad.TestInfrastructure;
using PSPad.Module.Investments.Import;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbCommentTests
{
    [Theory]
    [InlineData("OPEN BUY GHI.PL 1.5 @ 120", "1.5", "120")]
    [InlineData("CLOSE BUY JKL.PL 3 @ 80", "3", "80")]
    [InlineData("OPEN BUY ACM.PL 20 @ 1.5", "20", "1.5")]
    public void CommentsGiveQuantityAndPrice(string comment, string quantity, string price)
    {
        var parsed = XtbComment.Parse(comment);

        Assert.NotNull(parsed);
        Assert.Equal(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), parsed.Quantity);
        Assert.Equal(decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), parsed.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OPEN BUY ACM.PL twenty @ 1.5")]
    [InlineData("RST.US USD WHT 27%")]
    [InlineData("OPEN SELL ACM.PL 20 @ 1.5")]
    public void OtherCommentsGiveNothing(string? comment) =>
        Assert.Null(XtbComment.Parse(comment));
}
