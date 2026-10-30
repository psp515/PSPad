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

    const string Base = "http://localhost:5001/";

    [Fact]
    public void AReturnAddressInsideTheAppIsSafe()
    {
        var returnUrl = ReturnUrlQuery.SafeFrom(
            "http://localhost:5001/welcome?returnUrl=http%3A%2F%2Flocalhost%3A5001%2Finbox%3Fopen%3D1", Base);

        Assert.Equal("http://localhost:5001/inbox?open=1", returnUrl);
    }

    [Theory]
    [InlineData("%2Fgoals")]
    [InlineData("goals")]
    public void ARelativeReturnAddressResolvesInsideTheApp(string returnUrl)
    {
        Assert.Equal("http://localhost:5001/goals", ReturnUrlQuery.SafeFrom($"{Base}welcome?returnUrl={returnUrl}", Base));
    }

    [Theory]
    [InlineData("https%3A%2F%2Fevil.example%2Finbox")]
    [InlineData("%2F%2Fevil.example%2Finbox")]
    [InlineData("%2F%5Cevil.example%2Finbox")]
    [InlineData("http%3A%2F%2Flocalhost%3A5002%2Finbox")]
    [InlineData("http%3A%2F%2Flocalhost%3A5001.evil.example%2Finbox")]
    [InlineData("javascript%3Aalert(1)")]
    public void AReturnAddressOutsideTheAppIsDropped(string returnUrl)
    {
        Assert.Null(ReturnUrlQuery.SafeFrom($"{Base}welcome?returnUrl={returnUrl}", Base));
    }

    [Theory]
    [InlineData("http%3A%2F%2Flocalhost%3A5001%2Fwelcome")]
    [InlineData("%2Fwelcome%3FreturnUrl%3D%252Finbox")]
    [InlineData("http%3A%2F%2Flocalhost%3A5001%2Fauthentication%2Flogin")]
    public void AReturnAddressBackToSigningInIsDropped(string returnUrl)
    {
        Assert.Null(ReturnUrlQuery.SafeFrom($"{Base}welcome?returnUrl={returnUrl}", Base));
    }

    [Fact]
    public void NoReturnAddressStaysNothing()
    {
        Assert.Null(ReturnUrlQuery.SafeFrom($"{Base}welcome", Base));
    }
}
