using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Reading;

public static class Insights
{
    public const decimal HighRatio = 1.15m;
    public const decimal HighMinimum = 50m;
    public const decimal OnTrackTolerance = 0.10m;
    public const int Limit = 5;

    public static IReadOnlyList<Insight> For(MonthPace pace)
    {
        if (pace.Usual.IsEmpty)
        {
            return [];
        }

        var found = new List<Insight>();

        foreach (var category in pace.Categories)
        {
            if (category.UsualMonth is { } usualMonth && usualMonth > 0 && category.Spent > usualMonth)
            {
                found.Add(new Insight(InsightKind.PastUsual, category.Category, category.Spent, usualMonth, 0));
            }
            else if (category.UsualByNow is { } byNow && byNow > 0)
            {
                if (category.Spent >= byNow * HighRatio && category.Spent - byNow >= HighMinimum)
                {
                    found.Add(new Insight(InsightKind.RunningHigh, category.Category, category.Spent, byNow, 0));
                }
                else if (category.Spent > 0 && Math.Abs(category.Spent - byNow) <= byNow * OnTrackTolerance)
                {
                    found.Add(new Insight(InsightKind.OnTrack, category.Category, category.Spent, byNow, 0));
                }
            }
        }

        var seenThisMonth = pace.Totals.Days
            .SelectMany(day => day.Entries)
            .Where(entry => entry.Kind == CategoryKind.Income)
            .Select(entry => entry.Category)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        found.AddRange(pace.Usual.IncomeCategories
            .Where(income => income.MonthsSeen * 2 >= pace.Usual.SampledMonths
                             && !seenThisMonth.Contains(income.Category)
                             && income.TypicalDay >= pace.Day)
            .Select(income => new Insight(InsightKind.IncomeExpected, income.Category, income.Average, 0m, income.TypicalDay)));

        return
        [
            .. found
                .OrderBy(insight => insight.Kind)
                .ThenByDescending(insight => insight.Amount)
                .Take(Limit)
        ];
    }
}
