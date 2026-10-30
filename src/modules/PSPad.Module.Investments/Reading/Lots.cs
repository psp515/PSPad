using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public static class Lots
{
    public static (IReadOnlyList<Lot> Open, IReadOnlyList<ClosedResult> Closed, IReadOnlyDictionary<string, decimal> Oversold)
        Of(IEnumerable<InvestmentOperation> operations, DateTimeOffset? until = null)
    {
        var book = new LotBook();
        foreach (var operation in LiveOperations.Of(operations).Where(operation => until is null || operation.At <= until))
        {
            book.Apply(operation);
        }

        return (book.Open, book.Closed, book.Oversold);
    }
}
