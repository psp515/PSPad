using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.App.Tests.State;
using PSPad.App.Tests.State.Investments;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.App.Tests.Components.Investments;

[UnitTest]
public class OperationTextTests
{
    static readonly DateOnly Day = new(2026, 9, 14);

    static InvestmentOperation ATransfer(InvestmentAccount account, TransferType type, string? counterparty, MoneyValue? counter)
    {
        var operation = new InvestmentOperation();
        operation.ApplyAll(InvestmentOperation.Decide(null, account,
            new RecordInvestmentOperation(Guid.NewGuid(), Investing.Owner, Guid.NewGuid(), account.Id, OperationKind.TransferOut,
                new DateTimeOffset(Day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), null, null, null,
                new MoneyValue(1000m, "PLN", 1m, Day), null, OperationSource.Manual, null, null, type, counterparty, counter),
            Investing.Now));
        return operation;
    }

    [Fact]
    public void ATransferNamesItsTypeCounterpartyAndTheOtherSide()
    {
        using var culture = new CultureScope("en-US");
        var account = Investing.AnAccount("Main", "PLN");
        var operation = ATransfer(account, TransferType.CurrencyConversion, "70002", new MoneyValue(270m, "USD", 3.7m, Day));

        var text = OperationText.For(operation, "Main", null, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("Transfer out", text.Title);
        Assert.Equal("Currency conversion · 70002 · 270.00 USD · Main", text.Sub);
    }

    [Fact]
    public void AnIkeTransferWithoutCounterpartyNamesItsType()
    {
        using var culture = new CultureScope("en-US");
        var account = Investing.AnAccount("Main", "PLN");

        var text = OperationText.For(ATransfer(account, TransferType.Ike, null, null), "Main", null,
            TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("IKE transfer · Main", text.Sub);
    }
}
