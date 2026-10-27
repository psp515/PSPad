using System.Globalization;

namespace PSPad.App.State;

public sealed class TimelineAxis
{
    public const int FoldFrom = 3;
    public const int FoldSize = 28;
    const int HoursPerDay = 24;

    readonly int _units;
    readonly int _foldPixels;

    TimelineAxis(IReadOnlyList<TimelineSegment> segments)
    {
        Segments = segments;
        _units = segments.Where(segment => !segment.Folded).Sum(segment => segment.Weight);
        _foldPixels = segments.Count(segment => segment.Folded) * FoldSize;
    }

    public IReadOnlyList<TimelineSegment> Segments { get; }

    public static TimelineAxis For(IEnumerable<(int Start, int End)> spans, int? now, IReadOnlySet<int> unfolded)
    {
        var busy = spans.ToArray();
        var overlaps = Enumerable.Range(0, HoursPerDay)
            .Select(hour => busy.Count(span => span.Start < (hour + 1) * 60 && span.End > hour * 60))
            .ToArray();
        var nowHour = now / 60;
        bool Empty(int hour) => overlaps[hour] == 0 && hour != nowHour;

        var segments = new List<TimelineSegment>();
        var hour = 0;
        while (hour < HoursPerDay)
        {
            var end = hour;
            while (end < HoursPerDay && Empty(end))
            {
                end++;
            }

            if (end - hour >= FoldFrom && !unfolded.Contains(hour))
            {
                segments.Add(new TimelineSegment(hour, end, true, 0));
                hour = end;
                continue;
            }

            var last = Math.Max(end, hour + 1);
            for (; hour < last; hour++)
            {
                segments.Add(new TimelineSegment(hour, hour + 1, false, WeightOf(overlaps[hour], hour == nowHour)));
            }
        }

        return new TimelineAxis(segments);
    }

    public string At(int minutes)
    {
        var (units, pixels) = Offset(minutes);
        return Css(units, pixels);
    }

    public string Stretch(int from, int to)
    {
        var (startUnits, startPixels) = Offset(from);
        var (endUnits, endPixels) = Offset(to);
        return Css(endUnits - startUnits, endPixels - startPixels);
    }

    static int WeightOf(int overlaps, bool isNow) => overlaps switch
    {
        0 => isNow ? 2 : 1,
        < 3 => 2,
        _ => 3
    };

    (double Units, double Pixels) Offset(int minutes)
    {
        double units = 0;
        double pixels = 0;
        foreach (var segment in Segments)
        {
            var start = segment.StartHour * 60;
            var end = segment.EndHour * 60;
            var share = Math.Clamp((double)(minutes - start) / (end - start), 0, 1);
            if (segment.Folded)
            {
                pixels += FoldSize * share;
            }
            else
            {
                units += segment.Weight * share;
            }

            if (minutes <= end)
            {
                break;
            }
        }

        return (_units == 0 ? 0 : units / _units, pixels);
    }

    string Css(double share, double pixels) =>
        string.Create(CultureInfo.InvariantCulture,
            $"calc((100% - {_foldPixels}px) * {Math.Round(share, 5)} + {Math.Round(pixels, 2)}px)");
}
