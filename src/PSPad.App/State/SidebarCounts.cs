using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public sealed class SidebarCounts(
    IDocumentStore<TodoTask> tasks,
    IDocumentStore<Inbox> inboxes,
    AppState state)
{
    public int Today { get; private set; }

    public int Inbox { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync()
    {
        var all = await tasks.LoadAllAsync(state.UserId, CancellationToken.None);
        Today = TodayRule.Select(all, state.Today).Count;

        var inbox = (await inboxes.LoadAllAsync(state.UserId, CancellationToken.None)).FirstOrDefault();
        Inbox = inbox?.Items.Count ?? 0;

        Changed?.Invoke();
    }
}
