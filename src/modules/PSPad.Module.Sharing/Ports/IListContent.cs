using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Sharing.Ports;

public interface IListContent
{
    Task<(TaskList? List, IReadOnlyList<TodoTask> Tasks, IReadOnlyList<ReferenceItem> Items)> LoadAsync(
        Guid listId, CancellationToken ct);
}
