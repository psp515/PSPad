using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Presentation.Ordering;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State;

public sealed class ListPlacement
{
    readonly Guid _me;
    readonly Dictionary<Guid, Guid> _areaOf;
    readonly Dictionary<Guid, IReadOnlyList<TaskList>> _byArea;
    readonly Dictionary<Guid, string> _areaNames;

    ListPlacement(
        Guid me,
        Dictionary<Guid, Guid> areaOf,
        Dictionary<Guid, IReadOnlyList<TaskList>> byArea,
        Dictionary<Guid, string> areaNames,
        IReadOnlyList<TaskList> all)
    {
        _me = me;
        _areaOf = areaOf;
        _byArea = byArea;
        _areaNames = areaNames;
        All = all;
    }

    public static readonly ListPlacement Empty = For(Guid.Empty, [], [], [], []);

    public IReadOnlyList<TaskList> All { get; }

    public bool HasShared => InArea(SharedWithMe.AreaId).Count > 0;

    public static ListPlacement For(
        Guid me, IEnumerable<Area> areas, IEnumerable<TaskList> lists,
        IEnumerable<AreaView> areaViews, IEnumerable<ListView> listViews)
    {
        var liveAreas = areas.Where(area => !area.Deleted).OrderBy(area => area.Position).ToArray();
        var liveAreaIds = liveAreas.Select(area => area.Id).ToHashSet();
        var viewsByArea = areaViews.Where(view => !view.Deleted).ToDictionary(view => view.AreaId);
        var listViewByListId = listViews.ToDictionary(view => view.ListId);

        var mine = lists.Where(list => !list.Deleted && (list.UserId == me || list.HasMember(me))).ToArray();

        var areaOf = mine.ToDictionary(list => list.Id, list => AreaOf(list, me, listViewByListId, liveAreaIds));

        var byArea = new Dictionary<Guid, IReadOnlyList<TaskList>>();

        foreach (var area in liveAreas)
        {
            var inArea = mine.Where(list => areaOf[list.Id] == area.Id);
            byArea[area.Id] = Arranged.Sort(
                inArea, viewsByArea.GetValueOrDefault(area.Id)?.Order ?? [], list => list.Id, list => list.CreatedAt);
        }

        var shared = mine.Where(list => areaOf[list.Id] == SharedWithMe.AreaId);
        byArea[SharedWithMe.AreaId] = Arranged.Sort(shared, [], list => list.Id, list => list.CreatedAt);

        var areaNames = liveAreas.ToDictionary(area => area.Id, area => area.Name);
        areaNames[SharedWithMe.AreaId] = SharedWithMe.Name;

        var all = liveAreas
            .SelectMany(area => byArea[area.Id])
            .Concat(byArea[SharedWithMe.AreaId])
            .ToArray();

        return new ListPlacement(me, areaOf, byArea, areaNames, all);
    }

    static Guid AreaOf(
        TaskList list, Guid me, Dictionary<Guid, ListView> listViewByListId, HashSet<Guid> liveAreaIds)
    {
        if (list.UserId == me)
        {
            return list.AreaId;
        }

        if (listViewByListId.TryGetValue(list.Id, out var view) &&
            view.AreaId is { } filed && liveAreaIds.Contains(filed))
        {
            return filed;
        }

        return SharedWithMe.AreaId;
    }

    public Guid AreaOf(TaskList list) => _areaOf.GetValueOrDefault(list.Id, SharedWithMe.AreaId);

    public IReadOnlyList<TaskList> InArea(Guid areaId) => _byArea.GetValueOrDefault(areaId, []);

    public string AreaName(Guid areaId) => _areaNames.GetValueOrDefault(areaId, "");

    public bool IsMine(TaskList list) => list.UserId == _me;
}
