using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class DeletedAccountReadsTests
{
    static readonly FxBook Today = Rates(("USD", Day(1), 4.00m));

    readonly InvestmentAccount _kept = AnAccount(number: "2001");
    readonly InvestmentAccount _gone = AnAccount(number: "2002");
    readonly IReadOnlyList<InvestmentOperation> _keptOperations;
    readonly IReadOnlyList<InvestmentOperation> _goneOperations;

    public DeletedAccountReadsTests()
    {
        _keptOperations = [
            Deposit(_kept, 1000m, Day(1)),
            TransferOut(_kept, TransferType.Subaccount, 100m, Day(2), counterparty: "2002"),
        ];
        _goneOperations = [TransferIn(_gone, TransferType.Subaccount, 100m, Day(2), counterparty: "2001")];
        _gone.ApplyAll(InvestmentAccount.Decide(_gone, new DeleteInvestmentAccount(Guid.NewGuid(), Owner, _gone.Id), Now));
    }

    IEnumerable<(InvestmentAccount, IReadOnlyList<InvestmentOperation>)> Both =>
        [(_kept, _keptOperations), (_gone, _goneOperations)];

    [Fact]
    public void ATransferIntoADeletedAccountIsNoLongerAMoveBetweenOwnAccounts() =>
        Assert.Empty(InternalTransfers.Among(Both));

    [Fact]
    public void TheAllAccountsTotalsLeaveADeletedAccountOut()
    {
        var moves = InternalTransfers.Among(Both);
        var figures = PortfolioFigures.For(
            [.. Both.Select(entry => (entry.Item1, AccountFigures.For(entry.Item1, entry.Item2, Prices(), Today, Day(20), moves), entry.Item2))],
            Today, Day(20), moves);

        Assert.Equal(900m, figures.ValuePln);
        Assert.DoesNotContain(_gone.Id, figures.Accounts.Keys);
    }

    [Fact]
    public void TheValueOverTimeLeavesADeletedAccountOut()
    {
        var points = ValueOverTime.InPln(
            [.. Both.Select(entry => (entry.Item1, entry.Item2,
                ValueOverTime.For(entry.Item1, entry.Item2, Prices(), Today, Day(1), Day(3), TimeZoneInfo.Utc)))],
            Today, TimeZoneInfo.Utc);

        Assert.Equal(900m, points[^1].Value);
    }
}
