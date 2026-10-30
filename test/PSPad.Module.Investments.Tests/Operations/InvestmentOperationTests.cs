using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Operations;

[UnitTest]
public class InvestmentOperationTests
{
    static string Rejection(InvestmentOperation? operation, InvestmentAccount? account, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(operation, account, command, Now)).Message;

    static void Rejected(InvestmentAccount account, RecordInvestmentOperation command) =>
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account, command, Now));

    [Fact]
    public void RecordingKeepsTheOperationAndCountsIt()
    {
        var account = AnAccount();
        var command = ABuyCommand(account, ticker: " abc.us ") with { PositionId = " 77 ", Note = "  first  " };

        var operation = Recorded(account, command);

        Assert.Equal(command.OperationId, operation.Id);
        Assert.Equal(Owner, operation.UserId);
        Assert.Equal(account.Id, operation.AccountId);
        Assert.Equal(OperationKind.Buy, operation.Kind);
        Assert.Equal(TradedAt, operation.At);
        Assert.Equal("ABC.US", operation.Ticker);
        Assert.Equal(2m, operation.Quantity);
        Assert.Equal(50m, operation.Price);
        Assert.Equal(100m, operation.Money.Amount);
        Assert.Equal("77", operation.PositionId);
        Assert.Equal("first", operation.Note);
        Assert.Equal(OperationSource.Manual, operation.Source);
        Assert.Equal(Now, operation.RecordedAt);
        Assert.Equal(1, account.OperationCount);
    }

    [Fact]
    public void RecordingAKnownImportedIdChangesNothingEvenWhenDeleted()
    {
        var account = AnAccount();
        var operation = Delete(AnImportedBuy(account, xtbId: 900000001), account);
        var again = ImportedBuyCommand(account, xtbId: 900000001);

        Assert.Empty(InvestmentOperation.Decide(operation, account, again, Now));
        Assert.Empty(InvestmentAccount.Count(account, operation, again, Now));
    }

    [Fact]
    public void TheSameXtbIdGivesTheSameOperationIdOnEveryDevice() =>
        Assert.Equal(InvestmentOperation.IdFor(AccountId, 900000001), InvestmentOperation.IdFor(AccountId, 900000001));

    [Fact]
    public void DifferentXtbIdsGiveDifferentOperationIds() =>
        Assert.NotEqual(InvestmentOperation.IdFor(AccountId, 900000001), InvestmentOperation.IdFor(AccountId, 900000002));

    [Fact]
    public void AnImportedOperationWithTheWrongIdIsRejected()
    {
        var account = AnAccount();
        var command = ImportedBuyCommand(account, 900000001) with { OperationId = Guid.NewGuid() };

        Assert.Equal("An imported operation has the wrong id.", Rejection(null, account, command));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("99999999999999999999")]
    [InlineData(null)]
    public void AnImportedOperationNeedsANumericXtbId(string? externalId)
    {
        var account = AnAccount();
        var command = ImportedBuyCommand(account, 5) with { ExternalId = externalId };

        Assert.Equal("An imported operation has the wrong id.", Rejection(null, account, command));
    }

    [Fact]
    public void AnArchivedAccountRejectsARecord()
    {
        var account = Archive(AnAccount());

        Assert.Equal("This account is archived.", Rejection(null, account, ABuyCommand(account)));
    }

    [Fact]
    public void The20001stOperationIsRejected()
    {
        var account = AnAccount();
        for (var index = 0; index < InvestmentAccount.MaxOperations; index++)
        {
            account.ApplyAll(InvestmentAccount.Count(account, null, ACashCommand(account, OperationKind.Deposit), Now));
        }

        var rejection = Assert.Throws<DomainRejectedException>(() =>
            InvestmentAccount.Count(account, null, ACashCommand(account, OperationKind.Deposit), Now));

        Assert.Equal("An account can hold at most 20,000 operations.", rejection.Message);
    }

    [Fact]
    public void MoneyInAnotherCurrencyIsRejected()
    {
        var account = AnAccount("PLN");

        Assert.Equal("An operation is in the account's currency.",
            Rejection(null, account, ABuyCommand(account, money: Cash(100m, "USD"))));
    }

    [Fact]
    public void AStrangerCannotRecordIntoMyAccount()
    {
        var account = AnAccount();

        var rejection = Assert.Throws<DomainRejectedException>(() =>
            InvestmentOperation.Decide(null, account, ABuyCommand(account, userId: Stranger), Now));

        Assert.Equal("That account does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }

    [Theory]
    [InlineData(null, 2.0, 50.0)]
    [InlineData("ABC.US", null, 50.0)]
    [InlineData("ABC.US", 2.0, null)]
    [InlineData("ABC.US", 0.0, 50.0)]
    [InlineData("ABC.US", 2.0, 0.0)]
    [InlineData("ABC.US", -1.0, 50.0)]
    public void ABuyNeedsATickerAQuantityAndAPrice(string? ticker, double? quantity, double? price)
    {
        var account = AnAccount();

        Rejected(account, ABuyCommand(account, ticker: ticker, quantity: (decimal?)quantity, price: (decimal?)price));
        Rejected(account, ABuyCommand(account, ticker: ticker, quantity: (decimal?)quantity, price: (decimal?)price,
            kind: OperationKind.Sell));
    }

    [Theory]
    [InlineData(OperationKind.Dividend)]
    [InlineData(OperationKind.Tax)]
    public void ADividendOrTaxNeedsATickerAndNoQuantityOrPrice(OperationKind kind)
    {
        var account = AnAccount();

        Assert.Single(InvestmentOperation.Decide(null, account, ABuyCommand(account, quantity: null, price: null, kind: kind), Now));
        Rejected(account, ABuyCommand(account, ticker: null, quantity: null, price: null, kind: kind));
        Rejected(account, ABuyCommand(account, price: null, kind: kind));
        Rejected(account, ABuyCommand(account, quantity: null, kind: kind));
    }

    [Theory]
    [InlineData(OperationKind.Deposit)]
    [InlineData(OperationKind.Withdrawal)]
    [InlineData(OperationKind.Interest)]
    [InlineData(OperationKind.Fee)]
    public void CashOperationsHaveNoTickerQuantityOrPrice(OperationKind kind)
    {
        var account = AnAccount();

        Assert.Single(InvestmentOperation.Decide(null, account, ACashCommand(account, kind), Now));
        Rejected(account, ABuyCommand(account, quantity: null, price: null, kind: kind));
        Rejected(account, ABuyCommand(account, ticker: null, price: null, kind: kind));
        Rejected(account, ABuyCommand(account, ticker: null, quantity: null, kind: kind));
    }

    [Fact]
    public void QuantityAndPriceStayWithinTheirLimits()
    {
        var account = AnAccount();

        Assert.Single(InvestmentOperation.Decide(null, account,
            ABuyCommand(account, quantity: 1_000_000_000m, price: 0.00000001m), Now));
        Rejected(account, ABuyCommand(account, quantity: 1_000_000_000.1m));
        Rejected(account, ABuyCommand(account, price: 1_000_000_001m));
        Rejected(account, ABuyCommand(account, quantity: 0.000000001m));
        Rejected(account, ABuyCommand(account, price: 1.123456789m));
    }

    [Fact]
    public void ATickerOver24CharactersIsRejected()
    {
        var account = AnAccount();

        Rejected(account, ABuyCommand(account, ticker: new string('A', 25)));
    }

    [Fact]
    public void APositionIdOver32CharactersIsRejected()
    {
        var account = AnAccount();

        Rejected(account, ABuyCommand(account) with { PositionId = new string('1', 33) });
    }

    [Fact]
    public void ANoteOver1024CharactersIsRejected()
    {
        var account = AnAccount();

        Rejected(account, ABuyCommand(account) with { Note = new string('n', 1025) });
    }

    [Fact]
    public void BlankOptionalTextIsStoredAsNothing()
    {
        var account = AnAccount();

        var operation = Recorded(account, ABuyCommand(account) with { PositionId = "  ", Note = "  " });

        Assert.Null(operation.PositionId);
        Assert.Null(operation.Note);
    }

    [Fact]
    public void AMoneyWithoutAnAmountIsRejected()
    {
        var account = AnAccount();

        Rejected(account, ABuyCommand(account, money: Cash(0m)));
    }

    [Theory]
    [InlineData(OperationKind.Deposit, 100)]
    [InlineData(OperationKind.Interest, 100)]
    [InlineData(OperationKind.Withdrawal, -100)]
    [InlineData(OperationKind.Fee, -100)]
    public void CashSignsFollowTheKind(OperationKind kind, int signed) =>
        Assert.Equal(signed, SignedOf(kind));

    static decimal SignedOf(OperationKind kind)
    {
        var account = AnAccount();
        var command = kind is OperationKind.Deposit or OperationKind.Withdrawal or OperationKind.Interest or OperationKind.Fee
            ? ACashCommand(account, kind)
            : ABuyCommand(account, kind: kind,
                quantity: kind is OperationKind.Buy or OperationKind.Sell ? 2m : null,
                price: kind is OperationKind.Buy or OperationKind.Sell ? 50m : null);
        return Recorded(account, command).SignedAmount;
    }

    [Theory]
    [InlineData(OperationKind.Buy, -100)]
    [InlineData(OperationKind.Sell, 100)]
    [InlineData(OperationKind.Dividend, 100)]
    [InlineData(OperationKind.Tax, -100)]
    public void TradeSignsFollowTheKind(OperationKind kind, int signed) =>
        Assert.Equal(signed, SignedOf(kind));

    [Fact]
    public void EditingReplacesTheValuesButKeepsTheSource()
    {
        var account = AnAccount();
        var operation = AnImportedBuy(account, 42);
        var edit = EditOf(operation) with { Quantity = 3m, Note = "fixed", Money = Cash(150m) };

        operation.ApplyAll(InvestmentOperation.Decide(operation, account, edit, Now));

        Assert.Equal(3m, operation.Quantity);
        Assert.Equal("fixed", operation.Note);
        Assert.Equal(150m, operation.Money.Amount);
        Assert.Equal(OperationSource.Xtb, operation.Source);
        Assert.Equal("42", operation.ExternalId);
    }

    [Fact]
    public void AnIdenticalEditChangesNothing()
    {
        var account = AnAccount();
        var operation = Recorded(account, ABuyCommand(account));

        Assert.Empty(InvestmentOperation.Decide(operation, account, EditOf(operation), Now));
    }

    [Fact]
    public void AnEditIsValidatedLikeARecord()
    {
        var account = AnAccount();
        var operation = Recorded(account, ABuyCommand(account));

        Assert.Throws<DomainRejectedException>(() =>
            InvestmentOperation.Decide(operation, account, EditOf(operation) with { Ticker = null }, Now));
        Assert.Equal("An operation is in the account's currency.",
            Rejection(operation, account, EditOf(operation) with { Money = Cash(10m, "EUR") }));
    }

    [Fact]
    public void ADeletedOperationCannotBeEdited()
    {
        var account = AnAccount();
        var operation = Delete(Recorded(account, ABuyCommand(account)), account);

        var rejection = Assert.Throws<DomainRejectedException>(() =>
            InvestmentOperation.Decide(operation, account, EditOf(operation), Now));

        Assert.Equal("That operation does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }

    [Fact]
    public void DeletingTwiceChangesNothing()
    {
        var account = AnAccount();
        var operation = Delete(Recorded(account, ABuyCommand(account)), account);

        Assert.True(operation.Deleted);
        Assert.Empty(InvestmentOperation.Decide(operation, account,
            new DeleteInvestmentOperation(Guid.NewGuid(), Owner, operation.Id), Now));
    }

    [Fact]
    public void RestoringUndeletes()
    {
        var account = AnAccount();
        var operation = Delete(Recorded(account, ABuyCommand(account)), account);

        operation.ApplyAll(InvestmentOperation.Decide(operation, account,
            new RestoreInvestmentOperation(Guid.NewGuid(), Owner, operation.Id), Now));

        Assert.False(operation.Deleted);
    }

    [Fact]
    public void RestoringALiveOperationChangesNothing()
    {
        var account = AnAccount();
        var operation = Recorded(account, ABuyCommand(account));

        Assert.Empty(InvestmentOperation.Decide(operation, account,
            new RestoreInvestmentOperation(Guid.NewGuid(), Owner, operation.Id), Now));
    }

    [Fact]
    public void AnArchivedAccountRejectsDeleteAndRestore()
    {
        var account = AnAccount();
        var operation = Recorded(account, ABuyCommand(account));
        Archive(account);

        Assert.Equal("This account is archived.",
            Rejection(operation, account, new DeleteInvestmentOperation(Guid.NewGuid(), Owner, operation.Id)));
        Assert.Equal("This account is archived.",
            Rejection(operation, account, new RestoreInvestmentOperation(Guid.NewGuid(), Owner, operation.Id)));
    }

    [Fact]
    public void AStrangerCannotDeleteMyOperation()
    {
        var account = AnAccount();
        var operation = Recorded(account, ABuyCommand(account));

        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(operation, account,
            new DeleteInvestmentOperation(Guid.NewGuid(), Stranger, operation.Id), Now));
    }
}
