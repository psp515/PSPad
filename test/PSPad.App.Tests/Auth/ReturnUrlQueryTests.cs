using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class ReturnUrlQueryTests
{
    [Fact]
    public void ItReadsTheReturnAddressBack()
    {
        var returnUrl = ReturnUrlQuery.From(
            "http://localhost:5001/welcome?returnUrl=http%3A%2F%2Flocalhost%3A5001%2Finbox");

        Assert.Equal("http://localhost:5001/inbox", returnUrl);
    }

    [Fact]
    public void ItFindsTheReturnAddressBesideOtherParameters()
    {
        var returnUrl = ReturnUrlQuery.From("http://localhost:5001/welcome?a=1&returnUrl=%2Fgoals&b=2");

        Assert.Equal("/goals", returnUrl);
    }

    [Theory]
    [InlineData("http://localhost:5001/welcome")]
    [InlineData("http://localhost:5001/welcome?")]
    [InlineData("http://localhost:5001/welcome?other=1")]
    [InlineData("http://localhost:5001/welcome?returnUrl=")]
    public void ItReportsNothingWhenThereIsNoReturnAddress(string uri)
    {
        Assert.Null(ReturnUrlQuery.From(uri));
    }
}
