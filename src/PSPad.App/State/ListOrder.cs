using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.Ordering;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State;

public static class ListOrder
{
    public static IReadOnlyList<TaskList> InArea(Guid areaId, IEnumerable<TaskList> lists, AreaView? view) =>
        Arranged.Sort(
            lists.Where(list => !list.Deleted && list.AreaId == areaId),
            view?.Order ?? [],
            list => list.Id,
            list => list.CreatedAt);

    public static IReadOnlyList<TaskList> Arrange(
        IEnumerable<Area> areas, IEnumerable<TaskList> lists, IEnumerable<AreaView> views)
    {
        var all = lists.ToArray();
        var viewsByArea = views.Where(view => !view.Deleted).ToDictionary(view => view.AreaId);

        return areas
            .SelectMany(area => InArea(area.Id, all, viewsByArea.GetValueOrDefault(area.Id)))
            .ToArray();
    }
}
