using System.Globalization;

namespace PSPad.Module.Money.Values;

public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth Of(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    public YearMonth Next() => Of(FirstDay.AddMonths(1));

    public YearMonth Previous() => Of(FirstDay.AddMonths(-1));

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public int CompareTo(YearMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:D4}-{Month:D2}");
}
