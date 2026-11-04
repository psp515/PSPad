using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Investments.Tests.Import.Xlsx;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Import.Imports;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class InvestmentImportSharesReceivedTests
{
    const string Spun = "700000001";

    static readonly DateTimeOffset Received = Start.AddMinutes(30);

    static XtbClosedPosition SpunOff(decimal volume = 7m, string product = "IKE") =>
        new(Spun, "NEW.PL", "Newco SA", product, volume, 0.002m, Received, "OLD.PL spinoff");

    static XtbFile SpunOffAndSold() =>
        FileWith(Deposit(6, 100m, 900000001), Sale(7, 900000002, "NEW.PL", Spun, 7m, 25m, 175m, at: Start.AddDays(3))) with
        {
            ClosedPositions = [SpunOff()],
            OpenPositions = []
        };

    static IReadOnlyList<InvestmentOperation> Recorded(InvestmentAccount account, InvestmentImportPreview preview) =>
        [.. Plan(preview).OfType<RecordInvestmentOperation>().Select(command => Given.Recorded(account, command))];

    [Fact]
    public void AClosedPositionWithoutAPurchaseIsReceivedAndTheSellClosesIt()
    {
        var account = AnAccount("PLN", number: Number);

        var preview = Preview(SpunOffAndSold(), target: account);

        var row = Assert.Single(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
        Assert.Equal(ImportRowStatus.ToImport, row.Status);
        Assert.True(row.Ticked);
        Assert.Equal(InvestmentOperation.ReceivedIdFor(account.Id, long.Parse(Spun)), row.OperationId);
        Assert.Empty(preview.Errors);
        Assert.Empty(preview.OpenPositionDifferences!);

        var operations = Recorded(account, preview);
        var received = Assert.Single(operations, operation => operation.Kind == OperationKind.SharesReceived);
        Assert.Equal(("NEW.PL", 7m, 0.002m, 0.01m, Spun, "OLD.PL spinoff", Received),
            (received.Ticker, received.Quantity, received.Price, received.Money.Amount, received.PositionId, received.Note,
                received.At));
        Assert.Equal(OperationSource.Xtb, received.Source);
        var (open, closed, oversold) = Lots.Of(operations);
        Assert.Empty(open);
        Assert.Empty(oversold);
        Assert.Equal(174.99m, Assert.Single(closed).Result);
    }

    [Fact]
    public void AStillHeldSpunOffPositionIsReceivedFromOpenPositionsAndTheCheckAgrees()
    {
        var account = AnAccount("PLN", number: Number);
        var file = FileWith(Deposit(6, 100m, 900000001), Deposit(7, 50m, 900000002, at: Start.AddDays(2))) with
        {
            OpenPositions = [new XtbOpenPosition(Spun, "NEW.PL", 7m, 0.002m, Received, "IKE", "Newco SA")]
        };

        var preview = Preview(file, target: account);

        var row = Assert.Single(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
        Assert.Equal(("NEW.PL", "Newco SA", 7m, 0.002m, 0.01m), (row.Row.Ticker, row.Row.Instrument, row.Row.Quantity, row.Row.Price,
            row.Row.Amount));
        Assert.Empty(preview.OpenPositionDifferences!);
        Assert.Contains("NEW.PL", preview.NewTickers);
    }

    [Fact]
    public void APartlySoldSpunOffPositionIsReceivedOnceForBothParts()
    {
        var account = AnAccount("PLN", number: Number);
        var file = FileWith(Deposit(6, 100m, 900000001), Sale(7, 900000002, "NEW.PL", Spun, 3m, 25m, 75m, at: Start.AddDays(3))) with
        {
            ClosedPositions = [SpunOff(volume: 3m)],
            OpenPositions = [new XtbOpenPosition(Spun, "NEW.PL", 4m, 0.002m, Received, "IKE", "Newco SA")]
        };

        var preview = Preview(file, target: account);

        var row = Assert.Single(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
        Assert.Equal(7m, row.Row.Quantity);
        Assert.Empty(preview.OpenPositionDifferences!);
    }

    [Fact]
    public void APositionWithItsPurchaseRowsIsNotReceivedEvenWhenItsFillWasMerged()
    {
        const double time = 46300.5;
        const double minute = 1.0 / 1440;
        object?[] closedHeader = ["Instrument", "Ticker", "Volume", "Open Price", "Open Time (UTC)", "Product", "Position ID", "Comment"];
        var file = XtbParser.Parse(XlsxReader.Read(XlsxBuilder.Book(
            ("Cash Operations", XtbSheets.CashOperations(Number,
                XtbSheets.Row("Stock purchase", "Acme", "ACM.PL", time, -30.0, "1", "OPEN BUY 3/7 @ 10", "IKE", "800000011"),
                XtbSheets.Row("Stock purchase", "Acme", "ACM.PL", time + minute, -40.0, "2", "OPEN BUY 4/7 @ 10", "IKE", "800000012"),
                XtbSheets.Row("Stock purchase", "Acme", "ACM.PL", time, -20.0, "3", "OPEN BUY 2 @ 10", "IKE", "800000013"))),
            ("Closed Positions", [
                closedHeader,
                ["Acme", "ACM.PL", 4.0, 10.0, time + minute, "IKE", 800000012.0, null],
                ["Acme", "ACM.PL", 2.0, 10.0, time, "IKE", 800000013.0, null],
            ]))));

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.Equal(2, file.ClosedPositions.Count);
        Assert.DoesNotContain(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
    }

    [Fact]
    public void APositionBoughtInAnEarlierImportIsNotReceived()
    {
        var account = AnAccount("PLN", number: Number);
        var earlier = Given.Recorded(account, ImportedBuyCommand(account, 900000000) with { PositionId = Spun, Ticker = "NEW.PL" });

        var preview = Preview(SpunOffAndSold(), target: account, operations: [earlier]);

        Assert.DoesNotContain(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
    }

    [Fact]
    public void APositionOpenedBeforeTheCashOperationsStartIsNotReceived()
    {
        var file = SpunOffAndSold() with { ClosedPositions = [SpunOff() with { OpenTime = Start.AddDays(-30) }] };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.DoesNotContain(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
    }

    [Fact]
    public void ALotOpenedAfterTheLastCashRowIsNotReceived()
    {
        var file = FileWith(Deposit(6, 100m, 900000001)) with
        {
            OpenPositions = [new XtbOpenPosition(Spun, "NEW.PL", 7m, 0.002m, Start.AddDays(40), "IKE", "Newco SA")]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.DoesNotContain(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
    }

    [Fact]
    public void APurchaseOfAPositionAlreadyReceivedIsARowError()
    {
        var account = AnAccount("PLN", number: Number);
        var received = Recorded(account, Preview(SpunOffAndSold(), target: account))
            .Single(operation => operation.Kind == OperationKind.SharesReceived);
        var wider = FileWith(Deposit(6, 100m, 900000001), Purchase(7, 900000005, "NEW.PL", Spun, 7m, 1m, 7m));

        var preview = Preview(wider, target: account, operations: [received]);

        var error = Assert.Single(preview.Errors);
        Assert.Equal(7, error.Row.SheetRow);
        Assert.Equal([$"Position {Spun} was recorded as shares received."], error.Errors);
    }

    [Fact]
    public void OnlyStockLotsAreReceived()
    {
        var file = SpunOffAndSold() with
        {
            ClosedPositions = [SpunOff() with { Type = "SELL" }, SpunOff() with { PositionId = "700000002", Category = "CFD" }]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.DoesNotContain(preview.Rows, row => row.Row.Kind == XtbRowKind.SharesReceived);
    }

    [Fact]
    public void ReImportingKnowsTheReceivedSharesAndSendsNothingNew()
    {
        var account = AnAccount("PLN", number: Number);
        var first = Recorded(account, Preview(SpunOffAndSold(), target: account));

        var again = Preview(SpunOffAndSold(), target: account, operations: first);

        Assert.All(again.Rows, row => Assert.Equal(ImportRowStatus.AlreadyImported, row.Status));
        Assert.False(again.CanImport);
    }

    [Fact]
    public void ReceivedSharesGoToTheAccountOfTheirProduct()
    {
        var file = SplitFileWith(ToPlans(6, 900000001, 50m), IntoPlans(7, 900000002, 50m),
            Deposit(8, 10m, 900000003, at: Start.AddDays(2))) with
        {
            ClosedPositions = [SpunOff(product: "Investment Plans")]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.Contains(preview.Accounts.Single(section => section.AccountNumber == PlansNumber).Rows,
            row => row.Row.Kind == XtbRowKind.SharesReceived);
    }
}
