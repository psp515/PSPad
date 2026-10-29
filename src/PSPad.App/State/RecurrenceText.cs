using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Recurrence;

namespace PSPad.App.State;

public static class RecurrenceText
{
    public static string Describe(RecurrenceRule? rule, IStringLocalizer<TasksStrings> localizer) => rule switch
    {
        null => localizer["RepeatNever"],
        { Every: > 1 } r => DescribeInterval(r, localizer),
        { Kind: RecurrenceKind.Daily } => localizer["RepeatDaily"],
        { Kind: RecurrenceKind.Weekly, Days.Count: 5 } r when !r.Days.Contains(DayOfWeek.Saturday)
            && !r.Days.Contains(DayOfWeek.Sunday) => localizer["RepeatWeekdays"],
        { Kind: RecurrenceKind.Weekly } r => localizer["RepeatWeeklyOn", DayList(r)],
        { Kind: RecurrenceKind.MonthlyOnDay } r => localizer["RepeatMonthlyOnDay", r.DayOfMonth],
        { Kind: RecurrenceKind.Yearly } r => localizer["RepeatYearlyOn", YearlyDay(r.StartsOn)],
        _ => localizer["RepeatCustom"]
    };

    public static string DescribeTally(RepeatTally tally, IStringLocalizer<TasksStrings> localizer) => tally switch
    {
        { Total: 0 } => localizer["TallyNone"],
        { Streak: 0 } t => localizer.Plural(t.Total, "DoneTimes"),
        var t => localizer.Plural(t.Total, "DoneTimesStreak", t.Streak)
    };

    static string DescribeInterval(RecurrenceRule rule, IStringLocalizer<TasksStrings> localizer) => rule.Kind switch
    {
        RecurrenceKind.Daily => localizer.Plural(rule.Every, "EveryDays"),
        RecurrenceKind.Weekly => localizer.Plural(rule.Every, "EveryWeeks", DayList(rule)),
        RecurrenceKind.MonthlyOnDay => localizer.Plural(rule.Every, "EveryMonths", rule.DayOfMonth),
        RecurrenceKind.Yearly => localizer.Plural(rule.Every, "EveryYears", YearlyDay(rule.StartsOn)),
        _ => localizer["RepeatCustom"]
    };

    public static string YearlyDay(DateOnly day) => day.ToString("d MMM");

    public static string AbbreviatedDay(DayOfWeek day) => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day);

    static string DayList(RecurrenceRule rule) =>
        string.Join(", ", rule.Days.OrderBy(day => ((int)day + 6) % 7).Select(AbbreviatedDay));

    public static string UnitLabel(RecurrenceKind kind, int every, IStringLocalizer<TasksStrings> localizer) =>
        localizer.Plural(every, kind switch
        {
            RecurrenceKind.Daily => "UnitDays",
            RecurrenceKind.Weekly => "UnitWeeks",
            RecurrenceKind.Yearly => "UnitYears",
            _ => "UnitMonths"
        });
}
