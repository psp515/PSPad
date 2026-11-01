using PSPad.App.State.Investments;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class OperationKindsTests
{
    [Theory]
    [InlineData(OperationKind.Buy, true, true)]
    [InlineData(OperationKind.Dividend, true, true)]
    [InlineData(OperationKind.Tax, true, false)]
    [InlineData(OperationKind.TaxRefund, true, false)]
    [InlineData(OperationKind.TransferIn, false, false)]
    [InlineData(OperationKind.Deposit, false, false)]
    public void ATaxOrRefundMayNameATickerButOnlyTradesAndDividendsNeedOne(OperationKind kind, bool offered, bool needed)
    {
        Assert.Equal(offered, OperationKinds.HasTicker(kind));
        Assert.Equal(needed, OperationKinds.NeedsTicker(kind));
    }
}
