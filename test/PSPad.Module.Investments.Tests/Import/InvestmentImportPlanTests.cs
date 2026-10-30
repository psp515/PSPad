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
public class InvestmentImportPlanTests
{
    [Fact]
    public void ANewAccountIsCreatedFirstThenItsInstrumentsThenTheOperationsOldestFirst()
    {
        var file = FileWith(
            Sale(6, 900000003, "ACM.PL", "800000001", 1m, 12m, 12m, at: Start.AddDays(2)),
            Purchase(7, 900000002, "ACM.PL", "800000001", 2m, 10m, 20m, at: Start.AddDays(1)),
            Deposit(8, 100m, 900000001, at: Start));

        var commands = Plan(Preview(file), name: "My IKE");

        var create = Assert.IsType<CreateInvestmentAccount>(commands[0]);
        Assert.Equal((NewAccountId, "My IKE", "PLN", Broker.Xtb, Number),
            (create.AccountId, create.Name, create.Currency, create.Broker, create.BrokerAccountNumber));
        Assert.Equal("ACM.PL", Assert.IsType<EnsureInstrument>(commands[1]).Ticker);
        Assert.Equal(["900000001", "900000002", "900000003"],
            commands.Skip(2).Cast<RecordInvestmentOperation>().Select(record => record.ExternalId));
    }

    [Fact]
    public void EveryPlannedCommandIsAcceptedByTheAggregates()
    {
        var file = FileWith(
            Deposit(6, 100m, 900000001),
            Purchase(7, 900000002, "ACM.PL", "800000001", 2.5m, 10.123m, 25.31m),
            Commission(8, 900000003, "ACM.PL", 1m),
            Sale(9, 900000004, "ACM.PL", "800000001", 1m, 12m, 12m));

        var commands = Plan(Preview(file), currency: "PLN");

        var account = new InvestmentAccount();
        var portfolio = new InvestmentPortfolio();
        foreach (var command in commands)
        {
            switch (command)
            {
                case CreateInvestmentAccount create:
                    account.ApplyAll(InvestmentAccount.Decide(null, create, Now));
                    portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio, create, Now));
                    break;
                case EnsureInstrument ensure:
                    Assert.NotEmpty(Instrument.Decide(null, ensure, Now));
                    portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio, ensure, Now));
                    break;
                case RecordInvestmentOperation record:
                    Assert.NotEmpty(InvestmentOperation.Decide(null, account, record, Now));
                    account.ApplyAll(InvestmentAccount.Count(account, null, record, Now));
                    break;
            }
        }

        Assert.Equal(4, account.OperationCount);
        Assert.Equal(["ACM.PL"], portfolio.Tickers);
    }

    [Fact]
    public void AnImportedOperationCarriesItsXtbIdAndDeterministicId()
    {
        var account = AnAccount("PLN", number: Number);

        var record = Assert.IsType<RecordInvestmentOperation>(
            Assert.Single(Plan(Preview(FileWith(Purchase(7, 900000002, "ACM.PL", "800000001", 2m, 10m, 20m)),
                target: account))
                .OfType<RecordInvestmentOperation>()));

        Assert.Equal(InvestmentOperation.IdFor(account.Id, 900000002), record.OperationId);
        Assert.Equal(account.Id, record.AccountId);
        Assert.Equal(OperationSource.Xtb, record.Source);
        Assert.Equal("900000002", record.ExternalId);
        Assert.Equal((OperationKind.Buy, "ACM.PL", 2m, 10m, "800000001"),
            (record.Kind, record.Ticker, record.Quantity, record.Price, record.PositionId));
    }

    [Fact]
    public void AnAccountWithoutANumberTakesTheFilesNumberFirst()
    {
        var account = AnAccount("PLN");

        var commands = Plan(Preview(FileWith(Deposit(6, 100m, 900000001)), target: account));

        var set = Assert.IsType<SetBrokerAccountNumber>(commands[0]);
        Assert.Equal((account.Id, Number), (set.AccountId, set.Number));
        Assert.IsType<RecordInvestmentOperation>(commands[1]);
    }

    [Fact]
    public void APlnAmountIsAtRateOneOnItsDay()
    {
        var file = FileWith(Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)));

        var record = Assert.Single(Plan(Preview(file, target: AnAccount("PLN", number: Number)))
            .OfType<RecordInvestmentOperation>());

        Assert.Equal(new MoneyValue(100m, "PLN", 1m, new DateOnly(2026, 9, 2)), record.Money);
    }

    [Fact]
    public void AForeignAmountCarriesTheRateOfItsDayFrozen()
    {
        var file = FileWith(Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)));
        var rates = new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>
        {
            [new ImportRateKey("USD", new DateOnly(2026, 9, 2))] = (3.65m, new DateOnly(2026, 9, 1))
        };
        var preview = Preview(file, target: AnAccount("USD", number: Number), currency: "USD", rates: rates);

        var record = Assert.Single(Plan(preview, currency: "USD", rates: rates).OfType<RecordInvestmentOperation>());

        Assert.Equal(new MoneyValue(100m, "USD", 3.65m, new DateOnly(2026, 9, 1)), record.Money);
    }

    [Fact]
    public void ALongInstrumentNameIsCutToSixtyFourCharacters()
    {
        var name = new string('N', 80);
        var file = FileWith(Purchase(7, 900000002, "ACM.PL", "800000001", 2m, 10m, 20m, instrument: name));

        var ensure = Assert.Single(Plan(Preview(file, target: AnAccount("PLN", number: Number))).OfType<EnsureInstrument>());

        Assert.Equal(new string('N', 64), ensure.Name);
    }

    [Fact]
    public void AKnownTickerIsNotEnsuredAgain()
    {
        var file = FileWith(Purchase(7, 900000002, "T0.US", "800000001", 2m, 10m, 20m));

        var commands = Plan(Preview(file, target: AnAccount("PLN", number: Number), portfolio: APortfolioWith(tickers: 1)));

        Assert.Empty(commands.OfType<EnsureInstrument>());
    }

    [Fact]
    public void AFeeKeepsNoTicker()
    {
        var record = Assert.Single(Plan(Preview(FileWith(Commission(8, 900000003, "ACM.PL", 1m)),
            target: AnAccount("PLN", number: Number))).OfType<RecordInvestmentOperation>());

        Assert.Equal((OperationKind.Fee, (string?)null), (record.Kind, record.Ticker));
    }

    [Fact]
    public void APreviewThatCannotImportPlansNothing()
    {
        var commands = Plan(Preview(FileWith(Deposit(6, 100m, 900000001)),
            target: AnAccount("PLN", number: "87654321")));

        Assert.Empty(commands);
    }
}
