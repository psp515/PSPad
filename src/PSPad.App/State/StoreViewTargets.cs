using PSPad.Abstractions;
using PSPad.Module.Presentation;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State;

public sealed class StoreViewTargets(IDocumentStore<TaskList> lists, IDocumentStore<Area> areas) : IViewTargets
{
    public async Task RequireListAsync(Guid userId, Guid listId, CancellationToken ct) =>
        ListAccess.To(await lists.LoadAsync(listId, ct), userId);

    public async Task RequireAreaAsync(Guid userId, Guid areaId, CancellationToken ct) =>
        Area.Require(await areas.LoadAsync(areaId, ct), userId);
}
