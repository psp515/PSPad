using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Import.Imports;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class InvestmentImportPreviewTests
{
    [Fact]
    public void ErrorRowsAreListedWithTheirRowNumberAndTheValidRowsStillImport()
    {
        var file = FileWith(Broken(5, "Stock split", "Unknown type 'Stock split'."), Deposit(6, 1000m, id: 900000001));

        var preview = Preview(file, target: AnAccount("PLN"));

        var error = Assert.Single(preview.Errors);
        Assert.Equal(5, error.Row.SheetRow);
        Assert.Contains("Unknown type 'Stock split'.", error.Errors);
        Assert.False(error.Ticked);
        Assert.True(preview.CanImport);
        Assert.Single(Plan(preview).OfType<RecordInvestmentOperation>());
    }

    [Fact]
    public void AReImportMarksTheRowsAlreadyImportedAndLeavesNothingToSend()
    {
        var account = AnAccount("PLN", number: Number);
        var earlier = AnImportedBuy(account, 900000001);
        var file = FileWith(Purchase(7, 900000001, "ABC.US", "800000001", 2m, 50m, 100m));

        var preview = Preview(file, target: account, operations: [earlier]);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowStatus.AlreadyImported, row.Status);
        Assert.Equal(earlier.Id, row.OperationId);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void ARowDeletedEarlierStaysOutUntilTickedAndThenIsRestored()
    {
        var account = AnAccount("PLN", number: Number);
        var deleted = Delete(AnImportedBuy(account, 900000001), account);
        var file = FileWith(Purchase(7, 900000001, "ABC.US", "800000001", 2m, 50m, 100m));

        var untouched = Preview(file, target: account, operations: [deleted]);
        var ticked = Preview(file, target: account, operations: [deleted], tickedDeleted: new HashSet<int> { 7 });

        Assert.Equal(ImportRowStatus.DeletedEarlier, Assert.Single(untouched.Rows).Status);
        Assert.False(Assert.Single(untouched.Rows).Ticked);
        Assert.False(untouched.CanImport);
        Assert.True(Assert.Single(ticked.Rows).Ticked);
        var restore = Assert.Single(Plan(ticked));
        Assert.Equal(deleted.Id, Assert.IsType<RestoreInvestmentOperation>(restore).OperationId);
    }

    [Fact]
    public void AnUntickedRowIsNotSent()
    {
        var file = FileWith(Deposit(6, 1000m, 900000001), Deposit(7, 500m, 900000002));

        var preview = Preview(file, target: AnAccount("PLN"), unticked: new HashSet<int> { 6 });

        var record = Assert.Single(Plan(preview).OfType<RecordInvestmentOperation>());
        Assert.Equal("900000002", record.ExternalId);
    }

    [Fact]
    public void AFileWhoseNumberAnotherAccountHoldsIsAFileErrorAndSetsNoNumber()
    {
        var target = AnAccount("PLN");
        var owner = AnAccount("PLN", number: Number);

        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), target: target, accounts: [target, owner]);

        Assert.Equal("This file belongs to account IKE.", preview.FileError);
        Assert.Empty(Plan(preview));
    }

    [Fact]
    public void AFileFromAnotherAccountIsAFileError()
    {
        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), target: AnAccount("PLN", number: "87654321"));

        Assert.Equal("This file is from account 12345678.", preview.FileError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void TheFileNumberPicksTheAccount()
    {
        var other = AnAccount("PLN", number: "87654321");
        var matching = AnAccount("PLN", number: Number);

        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), accounts: [other, matching]);

        Assert.Same(matching, preview.Account);
        Assert.Null(preview.NewAccount);
        Assert.Equal(InvestmentOperation.IdFor(matching.Id, 900000001), Assert.Single(preview.Rows).OperationId);
    }

    [Fact]
    public void AnUnknownNumberProposesANewAccountNamedAfterTheProduct()
    {
        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), accounts: [AnAccount("PLN", number: "87654321")]);

        Assert.Null(preview.Account);
        Assert.Equal(new NewAccountProposal("IKE 12345678", Number), preview.NewAccount);
        Assert.Equal(ImportRowStatus.ToImport, Assert.Single(preview.Rows).Status);
        Assert.True(preview.CanImport);
    }

    [Fact]
    public void AFileWithoutAProductProposesAnXtbAccount()
    {
        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)) with { Product = null });

        Assert.Equal("XTB 12345678", preview.NewAccount?.Name);
    }

    [Fact]
    public void AThirtyThirdAccountIsNotOffered()
    {
        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), portfolio: APortfolioWith(accounts: 32));

        Assert.Null(preview.NewAccount);
        Assert.Equal("You can have at most 32 investment accounts.", preview.LimitError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void AnArchivedAccountTakesNoImport()
    {
        var preview = Preview(FileWith(Deposit(6, 1000m, 900000001)), target: Archive(AnAccount("PLN", number: Number)));

        Assert.Equal("This account is archived.", preview.FileError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void AnImportPastTwentyThousandOperationsSaysHowManyTooMany()
    {
        var account = Counted(AnAccount("PLN", number: Number), 20_000);
        var rows = Enumerable.Range(0, 1_240).Select(index => Deposit(index + 2, 10m, 900000001 + index)).ToArray();

        var preview = Preview(FileWith(rows), target: account);

        Assert.Equal("This import needs 1,240 more operations than the account can take.", preview.LimitError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void UntickingRowsBringsTheImportUnderTheOperationLimit()
    {
        var account = Counted(AnAccount("PLN", number: Number), 19_999);
        var file = FileWith(Deposit(6, 10m, 900000001), Deposit(7, 10m, 900000002));

        var over = Preview(file, target: account);
        var under = Preview(file, target: account, unticked: new HashSet<int> { 7 });

        Assert.Equal("This import needs 1 more operations than the account can take.", over.LimitError);
        Assert.Null(under.LimitError);
        Assert.True(under.CanImport);
    }

    [Fact]
    public void NewTickersPastFiveHundredInstrumentsSayHowManyTooMany()
    {
        var file = FileWith(
            Purchase(6, 900000001, "ACM.PL", "800000001", 1m, 10m, 10m),
            Purchase(7, 900000002, "VWX.PL", "800000002", 1m, 10m, 10m),
            Purchase(8, 900000003, "T0.US", "800000003", 1m, 10m, 10m));

        var preview = Preview(file, target: AnAccount("PLN", number: Number), portfolio: APortfolioWith(tickers: 499));

        Assert.Equal(["ACM.PL", "VWX.PL"], preview.NewTickers);
        Assert.Equal("This import needs 1 more instruments than you can have.", preview.LimitError);
        Assert.False(preview.CanImport);
    }

    [Fact]
    public void ARowInAForeignCurrencyWaitsForItsRate()
    {
        var file = FileWith(Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)));
        var account = AnAccount("USD", number: Number);
        var rates = new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>
        {
            [new ImportRateKey("USD", new DateOnly(2026, 9, 2))] = (3.65m, new DateOnly(2026, 9, 1))
        };

        var waiting = Preview(file, target: account, currency: "USD");
        var rated = Preview(file, target: account, currency: "USD", rates: rates);

        Assert.Equal(ImportRowStatus.WaitingForRate, Assert.Single(waiting.Rows).Status);
        Assert.False(waiting.CanImport);
        Assert.Equal(ImportRowStatus.ToImport, Assert.Single(rated.Rows).Status);
        Assert.True(rated.CanImport);
    }

    [Fact]
    public void TheRatesNeededAreOnePerCurrencyAndDayInTheUsersZone()
    {
        var file = FileWith(
            Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero)),
            Deposit(7, 100m, 900000002, at: new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)),
            Deposit(8, 100m, 900000003, at: new DateTimeOffset(2026, 9, 2, 6, 0, 0, TimeSpan.Zero)),
            Broken(9, "Stock split", "Unknown type 'Stock split'."));

        var needed = InvestmentImportPreviewer.RatesNeeded(file, "USD", Warsaw);

        Assert.Equal(
            [new ImportRateKey("USD", new DateOnly(2026, 9, 1)), new ImportRateKey("USD", new DateOnly(2026, 9, 2))],
            needed.OrderBy(key => key.Day));
    }

    [Fact]
    public void APlnAccountNeedsNoRates()
    {
        Assert.Empty(InvestmentImportPreviewer.RatesNeeded(FileWith(Deposit(6, 100m, 900000001)), "PLN", Warsaw));
    }

    [Fact]
    public void TheDayIsTheDateInTheUsersZone()
    {
        var file = FileWith(Deposit(6, 100m, 900000001, at: new DateTimeOffset(2026, 9, 1, 23, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 9, 2), Assert.Single(Preview(file, target: AnAccount("PLN")).Rows).Day);
    }

    [Fact]
    public void ATickerTheAccountCannotTakeIsARowError()
    {
        var file = FileWith(Purchase(6, 900000001, "ABCDEFGHIJKLMNOPQRSTUVWXY.US", "800000001", 1m, 10m, 10m));

        var row = Assert.Single(Preview(file, target: AnAccount("PLN")).Errors);

        Assert.Contains("A ticker is at most 24 characters.", row.Errors);
    }

    [Fact]
    public void AnIdTwiceInTheFileIsARowErrorOnTheSecond()
    {
        var file = FileWith(Deposit(6, 100m, 900000001), Deposit(7, 100m, 900000001));

        var row = Assert.Single(Preview(file, target: AnAccount("PLN")).Errors);

        Assert.Equal(7, row.Row.SheetRow);
        Assert.Contains("The ID 900000001 is already on row 6.", row.Errors);
    }

    [Fact]
    public void NothingTickedCannotImport()
    {
        var preview = Preview(FileWith(Deposit(6, 100m, 900000001)), target: AnAccount("PLN"),
            unticked: new HashSet<int> { 6 });

        Assert.False(preview.CanImport);
    }
}
