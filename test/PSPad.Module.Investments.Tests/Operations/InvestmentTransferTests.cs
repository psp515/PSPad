using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Operations;

[UnitTest]
public class InvestmentTransferTests
{
    static string Rejection(InvestmentAccount account, RecordInvestmentOperation command) =>
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account, command, Now)).Message;

    [Fact]
    public void ATransferKeepsItsTypeCounterpartyAndCounterAmount()
    {
        var account = AnAccount("USD");

        var operation = Recorded(account, ATransferCommand(account, counterparty: " 44445555 ",
            counterMoney: Cash(365.12m)));

        Assert.Equal(OperationKind.TransferIn, operation.Kind);
        Assert.Equal(TransferType.CurrencyConversion, operation.TransferType);
        Assert.Equal("44445555", operation.CounterpartyAccountNumber);
        Assert.Equal(365.12m, operation.CounterMoney!.Amount);
        Assert.Equal("PLN", operation.CounterMoney.Currency);
        Assert.Equal(100m, operation.SignedAmount);
    }

    [Fact]
    public void ATransferOutTakesCashOut()
    {
        var account = AnAccount();

        var operation = Recorded(account, ATransferCommand(account, OperationKind.TransferOut, TransferType.Ike,
            counterMoney: null));

        Assert.Equal(-100m, operation.SignedAmount);
        Assert.Equal(TransferType.Ike, operation.TransferType);
        Assert.Null(operation.CounterMoney);
    }

    [Fact]
    public void ACounterpartyAndACounterAmountAreOptional()
    {
        var account = AnAccount();

        var operation = Recorded(account, ATransferCommand(account, type: TransferType.Subaccount, counterparty: "  "));

        Assert.Null(operation.CounterpartyAccountNumber);
        Assert.Null(operation.CounterMoney);
    }

    [Theory]
    [InlineData(OperationKind.TransferIn)]
    [InlineData(OperationKind.TransferOut)]
    public void ATransferNeedsAType(OperationKind kind)
    {
        var account = AnAccount();

        Assert.Equal("A transfer needs a type.", Rejection(account, ATransferCommand(account, kind, type: null)));
        Assert.Equal("That transfer type does not exist.",
            Rejection(account, ATransferCommand(account, kind, type: (TransferType)42)));
    }

    [Fact]
    public void ATransferHasNoTickerQuantityOrPrice()
    {
        var account = AnAccount();

        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account,
            ATransferCommand(account) with { Ticker = "ABC.US" }, Now));
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account,
            ATransferCommand(account) with { Quantity = 1m }, Now));
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account,
            ATransferCommand(account) with { Price = 1m }, Now));
    }

    [Theory]
    [InlineData(OperationKind.Deposit)]
    [InlineData(OperationKind.Withdrawal)]
    [InlineData(OperationKind.Interest)]
    public void OnlyATransferCarriesTransferFields(OperationKind kind)
    {
        var account = AnAccount();
        var plain = ACashCommand(account, kind);

        Assert.Equal("Only a transfer has a transfer type.",
            Rejection(account, plain with { TransferType = TransferType.Ike }));
        Assert.Equal("Only a transfer has a counterparty account.",
            Rejection(account, plain with { CounterpartyAccountNumber = "44445555" }));
        Assert.Equal("Only a currency conversion has a counter amount.",
            Rejection(account, plain with { CounterMoney = Cash(10m, "USD") }));
    }

    [Theory]
    [InlineData(TransferType.Subaccount)]
    [InlineData(TransferType.Ike)]
    public void OnlyACurrencyConversionHasACounterAmount(TransferType type)
    {
        var account = AnAccount();

        Assert.Equal("Only a currency conversion has a counter amount.",
            Rejection(account, ATransferCommand(account, type: type, counterMoney: Cash(10m, "USD"))));
    }

    [Fact]
    public void ACounterAmountIsInAnotherCurrencyThanTheAccount()
    {
        var account = AnAccount("USD");

        Assert.Equal("A currency conversion's other side is in another currency.",
            Rejection(account, ATransferCommand(account, counterMoney: Cash(10m, "USD"))));
    }

    [Fact]
    public void ACounterAmountIsAValidAmount()
    {
        var account = AnAccount("USD");

        Assert.Equal("An amount has to be more than zero.",
            Rejection(account, ATransferCommand(account, counterMoney: Cash(0m))));
    }

    [Fact]
    public void ACounterpartyNumberOver32CharactersIsRejected()
    {
        var account = AnAccount();

        Assert.Equal("A counterparty account number is at most 32 characters.",
            Rejection(account, ATransferCommand(account, counterparty: new string('4', 33))));
    }

    [Fact]
    public void EditingReplacesTheTransferFields()
    {
        var account = AnAccount("USD");
        var operation = Recorded(account, ATransferCommand(account, counterMoney: Cash(365m)));
        var edit = EditOf(operation) with
        {
            Kind = OperationKind.TransferOut, TransferType = TransferType.Subaccount, CounterpartyAccountNumber = "55556666",
            CounterMoney = null
        };

        operation.ApplyAll(InvestmentOperation.Decide(operation, account, edit, Now));

        Assert.Equal(OperationKind.TransferOut, operation.Kind);
        Assert.Equal(TransferType.Subaccount, operation.TransferType);
        Assert.Equal("55556666", operation.CounterpartyAccountNumber);
        Assert.Null(operation.CounterMoney);
    }

    [Fact]
    public void AnIdenticalTransferEditChangesNothing()
    {
        var account = AnAccount("USD");
        var operation = Recorded(account, ATransferCommand(account, counterMoney: Cash(365m)));

        Assert.Empty(InvestmentOperation.Decide(operation, account, EditOf(operation), Now));
    }

    [Fact]
    public void AnOperationStoredBeforeTransfersReadsBackWithoutThem()
    {
        var json = "{\"accountId\":\"" + AccountId + "\",\"kind\":0,\"at\":\"2026-10-01T14:30:00+00:00\","
            + "\"money\":{\"amount\":100,\"currency\":\"PLN\",\"rateToPln\":1,\"rateDate\":\"2026-10-01\"}}";

        var operation = JsonSerializer.Deserialize<InvestmentOperation>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(OperationKind.Deposit, operation.Kind);
        Assert.Null(operation.TransferType);
        Assert.Null(operation.CounterpartyAccountNumber);
        Assert.Null(operation.CounterMoney);
    }

    [Fact]
    public void ACommandQueuedBeforeTransfersStillReads()
    {
        var json = "{\"commandId\":\"" + Guid.NewGuid() + "\",\"userId\":\"" + Owner + "\",\"operationId\":\"" + Guid.NewGuid()
            + "\",\"accountId\":\"" + AccountId + "\",\"kind\":0,\"at\":\"2026-10-01T14:30:00+00:00\","
            + "\"money\":{\"amount\":100,\"currency\":\"PLN\",\"rateToPln\":1,\"rateDate\":\"2026-10-01\"},\"source\":0}";

        var command = JsonSerializer.Deserialize<RecordInvestmentOperation>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(command.TransferType);
        Assert.Null(command.CounterpartyAccountNumber);
        Assert.Null(command.CounterMoney);
    }
}
