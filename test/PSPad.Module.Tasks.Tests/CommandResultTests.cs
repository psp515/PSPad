using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class CommandResultTests
{
    [Fact]
    public void ARejectionCarriesItsReasonAndWhetherARetryCouldEverSucceed()
    {
        var result = CommandResult.Rejected(new DomainRejectedException("That list no longer exists.", Unrecoverable: true));

        Assert.False(result.Accepted);
        Assert.Equal("That list no longer exists.", result.Rejection);
        Assert.True(result.Unrecoverable);
    }

    [Fact]
    public void AnOrdinaryRejectionMayStillSucceedOnRetry() =>
        Assert.False(CommandResult.Rejected(new DomainRejectedException("This budget is archived.")).Unrecoverable);
}
