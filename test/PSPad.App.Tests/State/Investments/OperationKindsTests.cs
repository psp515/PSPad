using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.App.Tests.Localization;
using PSPad.App.Tests.State;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class OperationKindsTests
{
    [Theory]
    [InlineData(OperationKind.Buy, true, true)]
    [InlineData(OperationKind.Dividend, true, true)]
    [InlineData(OperationKind.SharesReceived, true, true)]
    [InlineData(OperationKind.Tax, true, false)]
    [InlineData(OperationKind.TaxRefund, true, false)]
    [InlineData(OperationKind.Fee, true, false)]
    [InlineData(OperationKind.TransferIn, false, false)]
    [InlineData(OperationKind.Deposit, false, false)]
    public void ATaxRefundOrFeeMayNameATickerButOnlyTradesAndDividendsNeedOne(OperationKind kind, bool offered, bool needed)
    {
        Assert.Equal(offered, OperationKinds.HasTicker(kind));
        Assert.Equal(needed, OperationKinds.NeedsTicker(kind));
    }

    [Fact]
    public void SharesReceivedAskForQuantityAndPriceAndCostThem()
    {
        Assert.True(OperationKinds.Traded(OperationKind.SharesReceived));
        Assert.True(OperationKinds.CostIsComputed(OperationKind.SharesReceived));
        Assert.False(OperationKinds.CostIsComputed(OperationKind.Buy));
        Assert.Equal(0.01m, OperationKinds.ReceivedCost(7m, 0.002m));
        Assert.Equal(0m, OperationKinds.ReceivedCost(1m, 0.001m));
        Assert.Null(OperationKinds.ReceivedCost(null, 0.5m));
    }

    [Fact]
    public void EveryKindHasALabelInBothLanguages()
    {
        foreach (var culture in new[] { "en-US", "pl-PL" })
        {
            using var scope = new CultureScope(culture);
            var operations = TestLocalizers.For<InvestmentsStrings>();
            var import = TestLocalizers.For<InvestmentImportStrings>();
            foreach (var kind in OperationKinds.All)
            {
                Assert.False(operations[OperationKinds.Key(kind)].ResourceNotFound, $"{culture} {kind}");
                Assert.False(import[OperationKinds.Key(kind)].ResourceNotFound, $"{culture} import {kind}");
            }
        }
    }

    [Theory]
    [InlineData("en-US", "Shares received")]
    [InlineData("pl-PL", "Otrzymane akcje")]
    public void SharesReceivedAreNamed(string culture, string label)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(label, TestLocalizers.For<InvestmentsStrings>()["KindSharesReceived"].Value);
        Assert.Equal(label, TestLocalizers.For<InvestmentImportStrings>()["KindSharesReceived"].Value);
    }
}
