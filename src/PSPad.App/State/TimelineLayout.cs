using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public sealed record TimelineLayout(IReadOnlyList<TimelineBlock> Blocks, IReadOnlyList<TimelineGroup> Groups)
{
    const int LaneAtLeast = 48;
    const int CrossFrame = 54;
    const int LaneGap = 4;
    const int HalfHour = 30;
    const int MaxLanes = 2;

    public static readonly TimelineLayout Empty = new([], []);

    public int CrossLength
    {
        get
        {
            var stacked = Blocks.Select(block => block.Lanes * (LaneAtLeast + LaneGap))
                .Concat(Groups.Select(group => group.Lanes * (Math.Max(LaneAtLeast, group.Needs) + LaneGap)))
                .DefaultIfEmpty(0)
                .Max();
            return CrossFrame + stacked;
        }
    }

    public static TimelineLayout Of(IReadOnlyList<TodayEntry> entries)
    {
        var spans = entries
            .Where(entry => entry.Time is not null)
            .Select(TimelineSpan.Of)
            .OrderBy(span => span.Start).ThenByDescending(span => span.VisibleEnd)
            .ToList();
        var blocks = new List<TimelineBlock>();
        var groups = new List<TimelineGroup>();
        foreach (var cluster in Clusters(spans))
        {
            Place(cluster, blocks, groups);
        }

        return new TimelineLayout(blocks, groups);
    }

    static IEnumerable<List<TimelineSpan>> Clusters(IEnumerable<TimelineSpan> spans)
    {
        var cluster = new List<TimelineSpan>();
        var clusterEnd = -1;
        foreach (var span in spans)
        {
            if (span.Start >= clusterEnd && cluster.Count > 0)
            {
                yield return cluster;
                cluster = [];
            }

            cluster.Add(span);
            clusterEnd = cluster.Count == 1 ? span.VisibleEnd : Math.Max(clusterEnd, span.VisibleEnd);
        }

        if (cluster.Count > 0)
        {
            yield return cluster;
        }
    }

    static void Place(List<TimelineSpan> cluster, List<TimelineBlock> blocks, List<TimelineGroup> groups)
    {
        var lanes = Lanes(cluster);
        var laneCount = lanes.Max() + 1;
        if (laneCount <= MaxLanes)
        {
            blocks.AddRange(cluster.Select((span, index) => new TimelineBlock(span.Entry, span.Start, span.End, lanes[index], laneCount)));
        }
        else if (cluster.All(span => span.Start - cluster[0].Start < HalfHour))
        {
            groups.Add(GroupOf(cluster, 0, 1));
        }
        else
        {
            var first = cluster.Where((_, index) => lanes[index] == 0);
            var beside = cluster.Where((_, index) => lanes[index] > 0);
            blocks.AddRange(first.Select(span => new TimelineBlock(span.Entry, span.Start, span.End, 0, MaxLanes)));
            foreach (var inner in Clusters(beside))
            {
                if (inner.Count == 1)
                {
                    blocks.Add(new TimelineBlock(inner[0].Entry, inner[0].Start, inner[0].End, 1, MaxLanes));
                }
                else
                {
                    groups.Add(GroupOf(inner, 1, MaxLanes));
                }
            }
        }
    }

    static int[] Lanes(List<TimelineSpan> cluster)
    {
        var laneEnds = new List<int>();
        return [.. cluster.Select(span =>
        {
            var lane = laneEnds.FindIndex(laneEnd => laneEnd <= span.Start);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(span.VisibleEnd);
            }
            else
            {
                laneEnds[lane] = span.VisibleEnd;
            }

            return lane;
        })];
    }

    static TimelineGroup GroupOf(List<TimelineSpan> members, int lane, int lanes)
    {
        var ordered = members
            .OrderBy(span => span.Start)
            .ThenBy(span => span.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new TimelineGroup([.. ordered.Select(span => span.Entry)], ordered[0].Start, members.Max(span => span.VisibleEnd), lane, lanes);
    }
}
