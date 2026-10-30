using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public static class ValueOverTime
{
    const int DailyUpToDays = 366;

    public static IReadOnlyList<ValuePoint> For(
        InvestmentAccount account, IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyDictionary<string, PriceHistory> pricesByTicker, FxBook fx, DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        var live = LiveOperations.Of(operations);
        var book = new LotBook();
        var next = 0;
        var cash = 0m;
        var netDeposits = 0m;
        var points = new List<ValuePoint>();

        foreach (var day in Days(from, to))
        {
            for (; next < live.Count && LiveOperations.DayOf(live[next], zone) <= day; next++)
            {
                var operation = live[next];
                book.Apply(operation);
                cash += operation.SignedAmount;
                netDeposits += operation.Kind switch
                {
                    OperationKind.Deposit => operation.Money.Amount,
                    OperationKind.Withdrawal => -operation.Money.Amount,
                    _ => 0m
                };
            }

            var positions = Valuation.Positions(account.Id, account.Currency, book.Open, book.Oversold, pricesByTicker, fx, day);
            points.Add(new ValuePoint(day, cash + positions.Sum(position => position.Value), netDeposits));
        }

        return points;
    }

    public static IReadOnlyList<ValuePoint> InPln(
        IReadOnlyList<(InvestmentAccount Account, IReadOnlyList<ValuePoint> Points)> accounts, FxBook fx) =>
        accounts.SelectMany(account => account.Points.Select(point => (account.Account.Currency, Point: point)))
            .GroupBy(entry => entry.Point.Day)
            .OrderBy(day => day.Key)
            .Select(day =>
            {
                var priced = day.Select(entry => (entry.Point, Rate: fx.ToPln(entry.Currency, day.Key)))
                    .Where(entry => entry.Rate is not null)
                    .ToList();
                return new ValuePoint(day.Key,
                    priced.Sum(entry => Valuation.Money(entry.Point.Value * entry.Rate!.Value)),
                    priced.Sum(entry => Valuation.Money(entry.Point.NetDeposits * entry.Rate!.Value)));
            })
            .ToList();

    static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to)
    {
        var weekly = to.DayNumber - from.DayNumber > DailyUpToDays;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!weekly || day.DayOfWeek == DayOfWeek.Sunday || day == to)
            {
                yield return day;
            }
        }
    }
}
