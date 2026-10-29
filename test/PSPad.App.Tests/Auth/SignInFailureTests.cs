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
}
