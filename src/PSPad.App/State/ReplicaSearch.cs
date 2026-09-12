using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public sealed class ReplicaSearch(
    IDocumentStore<TodoTask> tasks,
    IDocumentStore<TaskList> lists,
    IDocumentStore<Area> areas)
{
    public async Task<IReadOnlyList<SearchHit>> FindAsync(Guid userId, string query)
    {
        var needle = query.Trim();

        if (needle.Length == 0)
        {
            return [];
        }

        var allAreas = await areas.LoadAllAsync(userId, CancellationToken.None);
        var allLists = await lists.LoadAllAsync(userId, CancellationToken.None);
        var allTasks = await tasks.LoadAllAsync(userId, CancellationToken.None);

        var areaNames = allAreas.Where(area => !area.Deleted).ToDictionary(area => area.Id, area => area.Name);
        var listsById = allLists.Where(list => !list.Deleted).ToDictionary(list => list.Id);

        var taskHits = allTasks
            .Where(task => !task.Deleted && Matches(task.Name, needle)
                && listsById.TryGetValue(task.ListId, out var list) && areaNames.ContainsKey(list.AreaId))
            .Select(task => new SearchHit(task.Id, task.Name, PathOf(task.ListId, listsById, areaNames), false));

        var listHits = listsById.Values
            .Where(list => areaNames.ContainsKey(list.AreaId) && Matches(list.Name, needle))
            .Select(list => new SearchHit(list.Id, list.Name, areaNames[list.AreaId], true));

        return [.. listHits, .. taskHits];
    }

    static bool Matches(string name, string needle) =>
        name.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

    static string PathOf(
        Guid listId, IReadOnlyDictionary<Guid, TaskList> lists, IReadOnlyDictionary<Guid, string> areas) =>
        lists.TryGetValue(listId, out var list)
            ? $"{areas.GetValueOrDefault(list.AreaId, "")} › {list.Name}"
            : "";
}
