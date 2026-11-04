using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

internal static class BalancePreviewer
{
    public static (IReadOnlyList<HoldingPreview> Rows, IReadOnlyList<MonthPreview> Months) Build(
        CsvParse<ParsedHoldingRow> parse, ImportContext context, RateBook rates, ICollection<RateRequest> missing,
        Func<Guid> newId)
    {
        var previews = parse.Errors
            .Select(error => new HoldingPreview(error.Line, null, null, ImportStatus.Error, error.Message))
            .ToList();
        var state = context.Snapshots
            .Where(snapshot => snapshot.BudgetId == context.Budget.Id && !snapshot.Deleted)
            .ToDictionary(snapshot => snapshot.Month, snapshot => snapshot.Holdings);
        var months = new List<MonthPreview>();

        foreach (var group in parse.Rows.GroupBy(row => row.Month).OrderBy(group => group.Key))
        {
            var month = group.Key;
            var isNew = !state.TryGetValue(month, out var existing);
            var start = isNew ? CarriedInto(month, state) : existing!;
            var final = start.ToList();
            var matched = new HashSet<Guid>();
            var changes = new List<HoldingChange>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var (accepted, adds, unchanged) = (0, 0, 0);

            foreach (var row in group)
            {
                if (!seen.Add(row.Name))
                {
                    previews.Add(Failed(row, $"Holding {row.Name} appears twice in {month}."));
                    continue;
                }

                var (money, request, failure) = ImportMoney.Resolve(
                    row.Amount, row.Currency ?? context.DefaultCurrency, row.Rate, month.LastDay, rates);

                if (request is not null)
                {
                    missing.Add(request);
                }

                if (failure is not null)
                {
                    previews.Add(Failed(row, failure));
                    continue;
                }

                if (money is null)
                {
                    previews.Add(new HoldingPreview(row.Line, row, null, ImportStatus.WaitingForRate, null));
                    continue;
                }

                var match = start.FirstOrDefault(holding =>
                    !matched.Contains(holding.HoldingId) &&
                    string.Equals(holding.Name, row.Name, StringComparison.OrdinalIgnoreCase));
                var full = isNew ? accepted >= BalanceSnapshot.MaxHoldings : match is null && start.Count + adds >= BalanceSnapshot.MaxHoldings;

                if (full)
                {
                    previews.Add(Failed(row, $"A month can have at most {BalanceSnapshot.MaxHoldings} holdings."));
                    continue;
                }

                accepted++;

                if (match is not null)
                {
                    matched.Add(match.HoldingId);
                    var edited = new Holding(match.HoldingId, row.Name, row.Type, money, match.InvestmentAccountId);

                    if (edited == match)
                    {
                        unchanged++;
                        previews.Add(new HoldingPreview(row.Line, row, money, ImportStatus.Unchanged, null));
                        continue;
                    }

                    final[final.FindIndex(holding => holding.HoldingId == match.HoldingId)] = edited;
                    changes.Add(new HoldingChange(HoldingChangeKind.Edit, edited, row.Line));
                }
                else
                {
                    var added = new Holding(newId(), row.Name, row.Type, money);
                    final.Add(added);
                    changes.Add(new HoldingChange(HoldingChangeKind.Add, added, row.Line));
                    adds++;
                }

                previews.Add(new HoldingPreview(row.Line, row, money, ImportStatus.ToImport, null));
            }

            if (isNew)
            {
                foreach (var dropped in start.Where(holding => !matched.Contains(holding.HoldingId)))
                {
                    final.RemoveAll(holding => holding.HoldingId == dropped.HoldingId);
                    changes.Add(new HoldingChange(HoldingChangeKind.Remove, dropped, null));
                }
            }

            state[month] = final;
            months.Add(new MonthPreview(month, isNew, [.. changes.OrderBy(change => change.Kind)], final.Count, unchanged));
        }

        return ([.. previews.OrderBy(preview => preview.Line)], months);
    }

    static IReadOnlyList<Holding> CarriedInto(YearMonth month, Dictionary<YearMonth, IReadOnlyList<Holding>> state) =>
        state.Keys.Where(earlier => earlier < month).OrderDescending().Select(earlier => state[earlier]).FirstOrDefault() ?? [];

    static HoldingPreview Failed(ParsedHoldingRow row, string message) =>
        new(row.Line, row, null, ImportStatus.Error, message);
}
