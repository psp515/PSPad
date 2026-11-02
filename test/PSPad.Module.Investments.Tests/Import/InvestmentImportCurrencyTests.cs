using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Import.Imports;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class InvestmentImportCurrencyTests
{
    static XtbFile InEuro(params XtbRow[] rows) =>
        FileWith(rows) with { ProductCurrencies = new Dictionary<string, string> { ["IKE"] = "EUR" } };

    [Fact]
    public void ANewAccountTakesTheCurrencyTheFileIsIn()
    {
        var preview = Preview(InEuro(Deposit(6, 1000m, 900000001)), currency: "PLN");

        var section = Assert.Single(preview.Accounts);
        Assert.NotNull(section.NewAccount);
        Assert.Equal("EUR", section.Currency);
        Assert.Equal("EUR", section.DetectedCurrency);
        Assert.Null(preview.FileError);
    }

    [Fact]
    public void WhenTheFileSaysNothingTheChosenCurrencyStays()
    {
        var section = Assert.Single(Preview(FileWith(Deposit(6, 1000m, 900000001)), currency: "USD").Accounts);

        Assert.Equal("USD", section.Currency);
        Assert.Null(section.DetectedCurrency);
    }

    [Fact]
    public void AFileInAnotherCurrencyThanTheAccountIsAFileError()
    {
        var preview = Preview(InEuro(Deposit(6, 1000m, 900000001)), target: AnAccount("PLN", number: Number));

        Assert.Equal("This file is in EUR, but account IKE is in PLN.", preview.FileError);
        Assert.False(preview.CanImport);
        Assert.Empty(Plan(preview));
    }

    [Fact]
    public void AFileInTheAccountsCurrencyImports()
    {
        var preview = Preview(InEuro(Deposit(6, 1000m, 900000001)), target: AnAccount("EUR", number: Number),
            rates: new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>
            {
                [new ImportRateKey("EUR", new DateOnly(2026, 9, 1))] = (4.2m, new DateOnly(2026, 8, 31)),
            });

        Assert.Null(preview.FileError);
        Assert.True(preview.CanImport);
    }

    [Fact]
    public void AFileMatchedToAnExistingAccountByNumberChecksItsCurrencyToo()
    {
        var preview = Preview(InEuro(Deposit(6, 1000m, 900000001)), accounts: [AnAccount("USD", number: Number)]);

        Assert.Equal("This file is in EUR, but account IKE is in USD.", preview.FileError);
    }

    [Fact]
    public void ADeletedAccountIsNoTargetSoItsFileImportsIntoANewAccount()
    {
        var deleted = AnAccount("PLN", number: Number);
        var earlier = AnImportedBuy(deleted, 900000001);
        deleted.ApplyAll(InvestmentAccount.Decide(deleted, new DeleteInvestmentAccount(Guid.NewGuid(), Owner, deleted.Id), Now));
        var file = FileWith(Purchase(7, 900000001, "ABC.US", "800000001", 2m, 50m, 100m));

        var preview = Preview(file, accounts: [deleted], operations: [earlier]);

        var section = Assert.Single(preview.Accounts);
        Assert.Null(section.Account);
        Assert.NotNull(section.NewAccount);
        Assert.Equal(ImportRowStatus.ToImport, Assert.Single(preview.Rows).Status);
        var record = Assert.Single(Plan(preview).OfType<RecordInvestmentOperation>());
        Assert.Equal(NewAccountId, record.AccountId);
        Assert.NotEqual(earlier.Id, record.OperationId);
    }
}
