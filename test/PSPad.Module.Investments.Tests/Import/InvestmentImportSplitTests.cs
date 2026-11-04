using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Import.Imports;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class InvestmentImportSplitTests
{
    static readonly Guid PlansAccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    static readonly XtbFile Split = SplitFileWith(
        Deposit(6, 1000m, 900000001),
        ToPlans(7, 900000002, 200m),
        IntoPlans(8, 900000003, 200m),
        InPlans(Purchase(9, 900000004, "ACM.PL", "800000001", 2m, 50m, 100m)));

    static Dictionary<string, NewAccountChoice> BothNew => new()
    {
        [Number] = new(NewAccountId, "Main"),
        [PlansNumber] = new(PlansAccountId, "Plans")
    };

    [Fact]
    public void AFileWithASubaccountPreviewsOneSectionPerAccountTheFilesOwnFirst()
    {
        var preview = Preview(Split);

        Assert.Equal([Number, PlansNumber], preview.Accounts.Select(section => section.AccountNumber));
        Assert.Equal([6, 7], preview.Accounts[0].Rows.Select(row => row.Row.SheetRow));
        Assert.Equal([8, 9], preview.Accounts[1].Rows.Select(row => row.Row.SheetRow));
        Assert.Equal([6, 7, 8, 9], preview.Rows.Select(row => row.Row.SheetRow));
    }

    [Fact]
    public void AnUnknownSubaccountIsOfferedAsANewAccountInTheFilesCurrency()
    {
        var main = AnAccount("USD", number: Number);

        var preview = Preview(Split, accounts: [main], currency: "PLN");

        var plans = preview.Accounts[1];
        Assert.Null(plans.Account);
        Assert.Equal(new NewAccountProposal($"Investment Plans {PlansNumber}", PlansNumber), plans.NewAccount);
        Assert.Equal("USD", plans.Currency);
    }

    [Fact]
    public void WhenTheFilesAccountIsNewItsSubaccountTakesThePickedCurrency()
    {
        var preview = Preview(Split, currency: "EUR");

        Assert.Equal(["EUR", "EUR"], preview.Accounts.Select(section => section.Currency));
        Assert.Equal(2, preview.NewAccounts.Count());
    }

    [Fact]
    public void AKnownSubaccountNumberPicksItsAccount()
    {
        var main = AnAccount("PLN", number: Number);
        var plans = AnAccount("PLN", number: PlansNumber);

        var preview = Preview(Split, accounts: [main, plans]);

        Assert.Same(main, preview.Accounts[0].Account);
        Assert.Same(plans, preview.Accounts[1].Account);
        Assert.Equal(InvestmentOperation.IdFor(plans.Id, 900000003), preview.Accounts[1].Rows[0].OperationId);
        Assert.Empty(preview.NewAccounts);
    }

    [Fact]
    public void ImportingIntoTheMainAccountSendsTheSubaccountRowsToTheirOwnAccount()
    {
        var main = AnAccount("PLN", number: Number);

        var commands = Plan(Preview(Split, target: main, accounts: [main]), newAccounts: BothNew);

        var create = Assert.Single(commands.OfType<CreateInvestmentAccount>());
        Assert.Equal((PlansAccountId, "Plans", "PLN", PlansNumber),
            (create.AccountId, create.Name, create.Currency, create.BrokerAccountNumber));
        var records = commands.OfType<RecordInvestmentOperation>().ToList();
        Assert.Equal([main.Id, main.Id, PlansAccountId, PlansAccountId], records.Select(record => record.AccountId));
    }

    [Fact]
    public void ImportingIntoTheSubaccountBindsItToItsNumber()
    {
        var plans = AnAccount("PLN", number: PlansNumber);
        var main = AnAccount("PLN", number: Number);

        var preview = Preview(Split, target: plans, accounts: [plans, main]);

        Assert.Same(main, preview.Accounts[0].Account);
        Assert.Same(plans, preview.Accounts[1].Account);
        Assert.Null(preview.FileError);
        Assert.True(preview.CanImport);
    }

    [Fact]
    public void ANumberMatchingNoneOfTheFilesIsAFileError()
    {
        var preview = Preview(Split, target: AnAccount("PLN", number: "11112222"));

        Assert.Equal("This file is from account 12345678.", preview.FileError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void BothNewAccountsAreCreatedBeforeAnyOperation()
    {
        var commands = Plan(Preview(Split), newAccounts: BothNew);

        Assert.Equal([NewAccountId, PlansAccountId], commands.Take(2).Cast<CreateInvestmentAccount>().Select(create => create.AccountId));
        Assert.IsType<EnsureInstrument>(commands[2]);
        Assert.All(commands.Skip(3), command => Assert.IsType<RecordInvestmentOperation>(command));
    }

    [Fact]
    public void AnAccountWithNoChosenNameTakesTheProposedOne()
    {
        var commands = Plan(Preview(Split), newAccounts: new Dictionary<string, NewAccountChoice>());

        Assert.Equal(["IKE 12345678", $"Investment Plans {PlansNumber}"],
            commands.OfType<CreateInvestmentAccount>().Select(create => create.Name));
    }

    [Fact]
    public void EveryPlannedCommandOfASplitFileIsAcceptedByTheAggregates()
    {
        var commands = Plan(Preview(Split), newAccounts: BothNew);

        var accounts = new Dictionary<Guid, InvestmentAccount>();
        var portfolio = new InvestmentPortfolio();
        foreach (var command in commands)
        {
            switch (command)
            {
                case CreateInvestmentAccount create:
                    var account = new InvestmentAccount();
                    account.ApplyAll(InvestmentAccount.Decide(null, create, Now));
                    accounts[create.AccountId] = account;
                    portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio, create, Now));
                    break;
                case EnsureInstrument ensure:
                    Assert.NotEmpty(Instrument.Decide(null, ensure, Now));
                    break;
                case RecordInvestmentOperation record:
                    Assert.NotEmpty(InvestmentOperation.Decide(null, accounts[record.AccountId], record, Now));
                    break;
            }
        }

        Assert.Equal(2, portfolio.AccountIds.Count);
    }

    [Fact]
    public void TheAccountCapCountsEveryAccountTheFileWouldCreate()
    {
        var preview = Preview(Split, portfolio: APortfolioWith(accounts: 31));

        Assert.NotNull(preview.Accounts[0].NewAccount);
        Assert.Null(preview.Accounts[1].NewAccount);
        Assert.Equal("You can have at most 32 investment accounts.", preview.LimitError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void TheOperationCapIsPerAccount()
    {
        var main = Counted(AnAccount("PLN", number: Number), 20_000);
        var plans = AnAccount("PLN", number: PlansNumber);
        var file = SplitFileWith(InPlans(Deposit(6, 10m, 900000001)));

        var preview = Preview(file, accounts: [main, plans]);

        Assert.Null(preview.LimitError);
        Assert.True(preview.CanImport);
    }

    [Fact]
    public void TheOpenPositionCheckSpansEveryAccountOfTheFile()
    {
        var file = SplitFileWith(
            Purchase(6, 900000001, "ACM.PL", "800000001", 2m, 10m, 20m),
            InPlans(Purchase(7, 900000002, "BCD.PL", "800000002", 3m, 10m, 30m))) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ACM.PL", 2m), new XtbOpenPosition("800000002", "BCD.PL", 3m)]
        };

        Assert.Empty(Preview(file).OpenPositionDifferences!);
    }

    [Fact]
    public void RatesAreNeededForEveryForeignSection()
    {
        var main = AnAccount("USD", number: Number);
        var file = SplitFileWith(
            Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)),
            InPlans(Deposit(7, 100m, 900000002, at: new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero))));

        var needed = InvestmentImportPreviewer.RatesNeeded(Preview(file, accounts: [main]));

        Assert.Equal(
            [new ImportRateKey("USD", new DateOnly(2026, 9, 1)), new ImportRateKey("USD", new DateOnly(2026, 9, 3))],
            needed.OrderBy(key => key.Day));
    }

    [Fact]
    public void AConversionImportsWithItsTypeCounterpartyAndCounterAmountAtTheImpliedRate()
    {
        var account = AnAccount("USD", number: Number);
        var day = new DateOnly(2026, 9, 1);
        var rates = new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>
        {
            [new ImportRateKey("USD", day)] = (3.7m, day)
        };
        var file = FileWith(Conversion(6, 900000001, XtbRowKind.TransferIn, 250m, 900m, "PLN"),
            Conversion(7, 900000002, XtbRowKind.TransferOut, 100m, 90m, "EUR"));

        var records = Plan(Preview(file, target: account, currency: "USD", rates: rates), rates: rates)
            .OfType<RecordInvestmentOperation>().ToList();

        Assert.Equal((OperationKind.TransferIn, TransferType.CurrencyConversion, "55553333"),
            (records[0].Kind, records[0].TransferType, records[0].CounterpartyAccountNumber));
        Assert.Equal(new MoneyValue(900m, "PLN", 1m, day), records[0].CounterMoney);
        Assert.Equal(new MoneyValue(90m, "EUR", 4.111111m, day), records[1].CounterMoney);
        Assert.All(records, record => Assert.NotEmpty(InvestmentOperation.Decide(null, account, record, Now)));
    }
}
