using PSPad.Module.Investments.Import;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Import.Imports;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class OpenPositionCheckTests
{
    [Fact]
    public void AFileWithoutAnOpenPositionsSheetIsNotChecked()
    {
        var preview = Preview(FileWith(Purchase(6, 900000001, "ACM.PL", "800000001", 2m, 10m, 20m)),
            target: AnAccount("PLN", number: Number));

        Assert.Null(preview.OpenPositionDifferences);
    }

    [Fact]
    public void OpenLotsMatchingTheSheetLeaveNoDifferences()
    {
        var file = FileWith(
            Purchase(6, 900000001, "ACM.PL", "800000001", 2m, 10m, 20m),
            Purchase(7, 900000002, "ACM.PL", "800000002", 3m, 10m, 30m),
            Sale(8, 900000003, "ACM.PL", "800000002", 3m, 11m, 33m)) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ACM.PL", 2m)]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.Empty(preview.OpenPositionDifferences!);
    }

    [Fact]
    public void AMismatchIsListedAndDoesNotBlockTheImport()
    {
        var file = FileWith(
            Purchase(6, 900000001, "ACM.PL", "800000001", 2m, 10m, 20m),
            Purchase(7, 900000002, "VWX.PL", "800000002", 5m, 10m, 50m)) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ACM.PL", 3m), new XtbOpenPosition("800000009", "STU.PL", 1m)]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number));

        Assert.Equal(
            [
                new OpenPositionDifference("800000001", "ACM.PL", 3m, 2m),
                new OpenPositionDifference("800000009", "STU.PL", 1m, 0m),
                new OpenPositionDifference("800000002", "VWX.PL", 0m, 5m)
            ],
            preview.OpenPositionDifferences);
        Assert.True(preview.CanImport);
    }

    [Fact]
    public void OperationsAlreadyInTheAccountCountTowardsTheOpenLots()
    {
        var account = AnAccount("PLN", number: Number);
        var earlier = Buy(account, "ACM.PL", "800000001", 2m, 10m, 20m, Day(1));
        var file = FileWith(Sale(8, 900000003, "ACM.PL", "800000001", 1m, 11m, 11m)) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ACM.PL", 1m)]
        };

        var preview = Preview(file, target: account, operations: [earlier]);

        Assert.Empty(preview.OpenPositionDifferences!);
    }

    [Fact]
    public void AnUntickedRowLeavesItsLotOut()
    {
        var file = FileWith(Purchase(6, 900000001, "ACM.PL", "800000001", 2m, 10m, 20m)) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ACM.PL", 2m)]
        };

        var preview = Preview(file, target: AnAccount("PLN", number: Number), unticked: new HashSet<int> { 6 });

        Assert.Equal([new OpenPositionDifference("800000001", "ACM.PL", 2m, 0m)], preview.OpenPositionDifferences);
    }

    [Fact]
    public void ARestoredRowCountsTowardsTheOpenLots()
    {
        var account = AnAccount("PLN", number: Number);
        var deleted = Delete(Recorded(account, ImportedBuyCommand(account, 900000001) with { PositionId = "800000001" }),
            account);
        var file = FileWith(Purchase(6, 900000001, "ABC.US", "800000001", 2m, 50m, 100m)) with
        {
            OpenPositions = [new XtbOpenPosition("800000001", "ABC.US", 2m)]
        };

        var preview = Preview(file, target: account, operations: [deleted], tickedDeleted: new HashSet<int> { 6 });

        Assert.Empty(preview.OpenPositionDifferences!);
    }
}
