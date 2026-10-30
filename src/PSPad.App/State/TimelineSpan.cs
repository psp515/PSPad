using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public sealed record TimelineSpan(TodayEntry Entry, int Start, int End, int VisibleEnd)
{
    const int HalfHour = 30;
    const int MinutesPerDay = 24 * 60;
    const int ShortestVisibleSpan = 45;

    public static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    public static TimelineSpan Of(TodayEntry entry)
    {
        var time = entry.Time!;
        var start = Minutes(time.Start);
        var end = time.Overnight ? MinutesPerDay
            : time.End is { } finish ? Math.Min(Minutes(finish), MinutesPerDay)
            : start + HalfHour;
        return new TimelineSpan(entry, start, end, Math.Min(MinutesPerDay, Math.Max(end, start + ShortestVisibleSpan)));
    }
}
