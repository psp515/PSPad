using PSPad.App.State.Investments;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class TransferFormTests
{
    static readonly DateOnly Day = new(2026, 9, 14);
    static readonly MoneyValue Dollars = new(250m, "USD", 3.9m, Day);

    [Fact]
    public void ATransferCannotBeSavedWithoutAType()
    {
        var form = TransferForm.Empty("USD");

        Assert.False(form.Complete(OperationKind.TransferIn, "USD"));
        Assert.True((form with { Type = TransferType.Subaccount }).Complete(OperationKind.TransferIn, "USD"));
        Assert.True(form.Complete(OperationKind.Deposit, "USD"));
    }

    [Fact]
    public void ACurrencyAccountProposesPlnAsTheOtherSideAndAPlnAccountLeavesItToPick()
    {
        Assert.Equal("PLN", TransferForm.Empty("USD").CounterCurrency);
        Assert.Null(TransferForm.Empty("PLN").CounterCurrency);
        Assert.DoesNotContain("USD", TransferForm.CounterCurrencies("USD"));
        Assert.Contains("PLN", TransferForm.CounterCurrencies("USD"));
    }

    [Fact]
    public void AConversionWithACounterAmountNeedsItsOtherCurrency()
    {
        var form = new TransferForm(TransferType.CurrencyConversion, null, 1000m, null);

        Assert.False(form.Complete(OperationKind.TransferOut, "PLN"));
        Assert.False((form with { CounterCurrency = "PLN" }).Complete(OperationKind.TransferOut, "PLN"));
        Assert.True((form with { CounterCurrency = "USD" }).Complete(OperationKind.TransferOut, "PLN"));
        Assert.False((form with { CounterCurrency = "USD", CounterAmount = 0m }).Complete(OperationKind.TransferOut, "PLN"));
        Assert.True((form with { CounterAmount = null }).Complete(OperationKind.TransferOut, "PLN"));
    }

    [Fact]
    public void AConversionSendsItsTypeCounterpartyAndCounterAmountAtTheImpliedRate()
    {
        var form = new TransferForm(TransferType.CurrencyConversion, "  70001 ", 975m, "PLN");

        var fields = form.Fields(OperationKind.TransferIn, Dollars, Day);

        Assert.Equal(TransferType.CurrencyConversion, fields.Type);
        Assert.Equal("70001", fields.Counterparty);
        Assert.Equal(new MoneyValue(975m, "PLN", 1m, Day), fields.Counter);
    }

    [Fact]
    public void OnlyAConversionKeepsACounterAmountAndABlankCounterpartyIsNone()
    {
        var form = new TransferForm(TransferType.Ike, " ", 975m, "PLN");

        var fields = form.Fields(OperationKind.TransferOut, Dollars, Day);

        Assert.Equal(TransferType.Ike, fields.Type);
        Assert.Null(fields.Counterparty);
        Assert.Null(fields.Counter);
    }

    [Fact]
    public void AnotherKindSendsNoTransferFields()
    {
        var form = new TransferForm(TransferType.CurrencyConversion, "70001", 975m, "PLN");

        var fields = form.Fields(OperationKind.Deposit, Dollars, Day);

        Assert.Equal((null, null, null), fields);
    }
}
