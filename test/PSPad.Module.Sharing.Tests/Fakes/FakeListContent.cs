using PSPad.Module.Sharing.Ports;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Sharing.Tests.Fakes;

public sealed class FakeListContent : IListContent
{
    public TaskList? List { get; set; }
    public IReadOnlyList<TodoTask> Tasks { get; set; } = [];
    public IReadOnlyList<ReferenceItem> Items { get; set; } = [];

    public Task<(TaskList? List, IReadOnlyList<TodoTask> Tasks, IReadOnlyList<ReferenceItem> Items)> LoadAsync(
        Guid listId, CancellationToken ct) =>
        Task.FromResult((List, Tasks, Items));
}
