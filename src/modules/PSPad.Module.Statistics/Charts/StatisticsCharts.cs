namespace PSPad.Module.Statistics;

public static class StatisticsCharts
{
    const string NoGoal = "No goal";
    const string UnknownGoal = "(unknown goal)";
    const int WeekLength = 7;

    public static IReadOnlyList<DailyCompletions> Completions(
        IReadOnlyList<StatisticsRecord> records, DateOnly today, int days, TimeZoneInfo zone)
    {
        var planned = new Dictionary<DateOnly, int>();
        var unplanned = new Dictionary<DateOnly, int>();

        foreach (var completion in CompletionDays(records, zone))
        {
            var bucket = completion.Planned ? planned : unplanned;
            bucket[completion.Day] = bucket.GetValueOrDefault(completion.Day) + 1;
        }

        return Range(today, days)
            .Select(day => new DailyCompletions(
                day, planned.GetValueOrDefault(day), unplanned.GetValueOrDefault(day)))
            .ToList();
    }

    public static IReadOnlyList<DailyCount> Opened(
        IReadOnlyList<StatisticsRecord> records, DateOnly today, int days, TimeZoneInfo zone)
    {
        var counts = new Dictionary<DateOnly, int>();

        foreach (var record in records.Where(record => record.Kind == RecordKind.Created))
        {
            var day = DayOf(record.At, zone);
            counts[day] = counts.GetValueOrDefault(day) + 1;
        }

        return Range(today, days)
            .Select(day => new DailyCount(day, counts.GetValueOrDefault(day)))
            .ToList();
    }

    // openingOpen counts only records older than the oldest one passed in, so records that
    // predate the first charted day are folded into it here rather than counted twice.
    public static IReadOnlyList<DailyCount> Outstanding(
        IReadOnlyList<StatisticsRecord> records, DateOnly today, int days, TimeZoneInfo zone,
        int openingOpen)
    {
        var first = today.AddDays(1 - days);
        var deltas = new Dictionary<DateOnly, int>();
        var running = openingOpen;

        foreach (var record in records)
        {
            var delta = OpenDelta(record.Kind);

            if (delta == 0)
            {
                continue;
            }

            var day = DayOf(record.At, zone);

            if (day < first)
            {
                running += delta;
            }
            else if (day <= today)
            {
                deltas[day] = deltas.GetValueOrDefault(day) + delta;
            }
        }

        var points = new List<DailyCount>();

        foreach (var day in Range(today, days))
        {
            running += deltas.GetValueOrDefault(day);
            points.Add(new DailyCount(day, running));
        }

        return points;
    }

    public static IReadOnlyList<GoalTotal> ByGoal(
        IReadOnlyList<StatisticsRecord> records, IReadOnlyList<StatisticsLabel> labels)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var label in labels.Where(label => label.Kind == LabelKind.Goal))
        {
            names[label.Id] = label.Name;
        }

        var totals = new Dictionary<Guid, int>();
        var withoutGoal = 0;

        foreach (var record in records
                     .Where(record => record.Kind == RecordKind.Completed)
                     .Concat(FinalOccurrenceTicks(records)))
        {
            if (record.GoalId is { } goalId)
            {
                totals[goalId] = totals.GetValueOrDefault(goalId) + 1;
            }
            else
            {
                withoutGoal++;
            }
        }

        return totals
            .Select(total => new GoalTotal(total.Key, NameOf(total.Key, names), total.Value))
            .Append(new GoalTotal(null, NoGoal, withoutGoal))
            .OrderByDescending(bar => bar.Count)
            .ThenBy(bar => bar.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<DailyCount> Heatmap(
        IReadOnlyList<StatisticsRecord> records, DateOnly today, int days, TimeZoneInfo zone) =>
        Completions(records, today, days, zone)
            .Select(point => new DailyCount(point.Day, point.Planned + point.Unplanned))
            .ToList();

    public static StatisticsTiles Tiles(
        IReadOnlyList<StatisticsRecord> records, DateOnly today, int days, TimeZoneInfo zone)
    {
        var completions = CompletionDays(records, zone).ToList();
        var first = today.AddDays(1 - days);
        var weekStart = today.AddDays(1 - WeekLength);

        return new StatisticsTiles(
            completions.Count(completion => completion.Day == today),
            records.Count(record =>
                record.Kind == RecordKind.Created && DayOf(record.At, zone) == today),
            completions.Count(completion =>
                completion.Day >= weekStart && completion.Day <= today),
            records
                .Where(record => Within(DayOf(record.At, zone), first, today))
                .Sum(record => OpenDelta(record.Kind)));
    }

    static IEnumerable<(DateOnly Day, bool Planned)> CompletionDays(
        IReadOnlyList<StatisticsRecord> records, TimeZoneInfo zone)
    {
        foreach (var record in records.Where(record => record.Kind == RecordKind.Completed))
        {
            var day = DayOf(record.At, zone);

            yield return (day, record.DueOn is not null && record.DueOn <= day);
        }

        foreach (var tick in FinalOccurrenceTicks(records))
        {
            yield return (tick.OccurrenceDay!.Value, true);
        }
    }

    static IEnumerable<StatisticsRecord> FinalOccurrenceTicks(
        IReadOnlyList<StatisticsRecord> records) =>
        records
            .Where(record => record.OccurrenceDay is not null && IsOccurrence(record.Kind))
            .GroupBy(record => (record.TaskId, record.OccurrenceDay))
            .Select(group => group.MaxBy(record => record.Id)!)
            .Where(record => record.Kind == RecordKind.OccurrenceTicked);

    static bool IsOccurrence(RecordKind kind) =>
        kind is RecordKind.OccurrenceTicked or RecordKind.OccurrenceUnticked;

    static int OpenDelta(RecordKind kind) => kind switch
    {
        RecordKind.Created or RecordKind.Reopened => 1,
        RecordKind.Completed or RecordKind.Deleted => -1,
        _ => 0
    };

    static string NameOf(Guid goalId, IReadOnlyDictionary<Guid, string> names) =>
        names.TryGetValue(goalId, out var name) ? name : UnknownGoal;

    static bool Within(DateOnly day, DateOnly first, DateOnly last) =>
        day >= first && day <= last;

    static IEnumerable<DateOnly> Range(DateOnly today, int days) =>
        Enumerable.Range(0, Math.Max(days, 0)).Select(offset => today.AddDays(offset + 1 - days));

    static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
}
