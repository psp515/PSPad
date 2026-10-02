using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Recurrence;

public sealed record RecurrenceRule(
    RecurrenceKind Kind,
    DateOnly StartsOn,
    IReadOnlyList<DayOfWeek> Days,
    int DayOfMonth,
    int Interval = 1)
{
    // Documents stored before the interval existed read it back as 0.
    public int Every => Interval < 1 ? 1 : Interval;

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

    public static RecurrenceRule Yearly(DateOnly startsOn) =>
        new(RecurrenceKind.Yearly, startsOn, [], 0);

    public RecurrenceRule EveryNth(int interval) =>
        interval is < 1 or > 99
            ? throw new DomainRejectedException("A repeat interval must be between 1 and 99.")
            : this with { Interval = interval };

    public bool OccursOn(DateOnly day)
    {
        if (day < StartsOn)
        {
            return false;
        }

        return Kind switch
        {
            RecurrenceKind.Daily => (day.DayNumber - StartsOn.DayNumber) % Every == 0,
            RecurrenceKind.Weekly => Days.Contains(day.DayOfWeek)
                && (MondayOf(day).DayNumber - MondayOf(StartsOn).DayNumber) / 7 % Every == 0,
            RecurrenceKind.MonthlyOnDay => day.Day == EffectiveDayIn(day.Year, day.Month)
                && (MonthIndexOf(day) - MonthIndexOf(StartsOn)) % Every == 0,
            RecurrenceKind.Yearly => day.Month == StartsOn.Month
                && day.Day == Math.Min(StartsOn.Day, DateTime.DaysInMonth(day.Year, day.Month))
                && (day.Year - StartsOn.Year) % Every == 0,
            _ => false
        };
    }

    static DateOnly MondayOf(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    static int MonthIndexOf(DateOnly day) => day.Year * 12 + day.Month;

    int EffectiveDayIn(int year, int month) =>
        Math.Min(DayOfMonth, DateTime.DaysInMonth(year, month));
}
