using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class SignInFailureTests
{
    [Fact]
    public void NothingHasFailedBeforeASignInIsAttempted()
    {
        Assert.False(new SignInFailure().Happened);
    }

    [Fact]
    public void AFailedSignInIsRemembered()
    {
        var failure = new SignInFailure();

        failure.Record();

        Assert.True(failure.Happened);
    }

    [Fact]
    public void ClearingForgetsAnEarlierFailure()
    {
        var failure = new SignInFailure();
        failure.Record();

        failure.Clear();

        Assert.False(failure.Happened);
    }

    [Fact]
    public void TakingAFailureReportsItOnce()
    {
        var failure = new SignInFailure();
        failure.Record();

        Assert.True(failure.Take());
        Assert.False(failure.Take());
        Assert.False(failure.Happened);
    }

    [Fact]
    public void TakingWithNoFailureReportsNothing()
    {
        Assert.False(new SignInFailure().Take());
    }
}
