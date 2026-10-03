using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public static class ListAccessLoading
{
    public static async Task<ListAccess> AccessAsync(
        this IDocumentStore<TaskList> lists, Guid? listId, Guid actorId, CancellationToken ct) =>
        listId is { } id && await lists.LoadAsync(id, ct) is { } list
            ? ListAccess.To(list, actorId)
            : ListAccess.Owner(actorId);
}
