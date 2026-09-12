using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Recurrence;

public sealed record RecurrenceRule(
    RecurrenceKind Kind,
    DateOnly StartsOn,
    IReadOnlyList<DayOfWeek> Days,
    int DayOfMonth)
{
    public static RecurrenceRule Daily(DateOnly startsOn) =>
        new(RecurrenceKind.Daily, startsOn, [], 0);

    public static RecurrenceRule Weekly(DateOnly startsOn, params DayOfWeek[] days) =>
        days.Length == 0
            ? throw new DomainRejectedException("A weekly repeat needs at least one day.")
            : new RecurrenceRule(RecurrenceKind.Weekly, startsOn, days.Distinct().ToArray(), 0);

    public static RecurrenceRule MonthlyOnDay(DateOnly startsOn, int dayOfMonth) =>
        dayOfMonth is < 1 or > 31
            ? throw new DomainRejectedException("A monthly repeat needs a day between 1 and 31.")
            : new RecurrenceRule(RecurrenceKind.MonthlyOnDay, startsOn, [], dayOfMonth);

    public bool OccursOn(DateOnly day)
    {
        if (day < StartsOn)
        {
            return false;
        }

        return Kind switch
        {
            RecurrenceKind.Daily => true,
            RecurrenceKind.Weekly => Days.Contains(day.DayOfWeek),
            RecurrenceKind.MonthlyOnDay => day.Day == EffectiveDayIn(day.Year, day.Month),
            _ => false
        };
    }

    int EffectiveDayIn(int year, int month) =>
        Math.Min(DayOfMonth, DateTime.DaysInMonth(year, month));
}
