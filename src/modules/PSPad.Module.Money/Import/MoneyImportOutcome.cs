using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Import;

public sealed record MoneyImportOutcome(int Entries, int Holdings, int Months, IReadOnlyList<CsvRowError> Rejected)
{
    public static MoneyImportOutcome From(IReadOnlyList<PlannedCommand> plan, IReadOnlyList<CommandResult> results)
    {
        var landed = plan.Zip(results).Where(pair => pair.Second.Accepted).Select(pair => pair.First.Command).ToArray();

        return new MoneyImportOutcome(
            landed.Count(command => command is RecordExpense or RecordIncome),
            landed.Count(command => command is AddHolding or EditHolding),
            landed.Select(MonthOf).OfType<Values.YearMonth>().Distinct().Count(),
            [.. plan.Zip(results)
                .Where(pair => !pair.Second.Accepted)
                .Select(pair => new CsvRowError(pair.First.Line ?? 0, pair.Second.Rejection ?? "Rejected."))]);
    }

    static Values.YearMonth? MonthOf(ICommand command) => command switch
    {
        AddHolding add => add.Month,
        EditHolding edit => edit.Month,
        _ => null
    };
}
