using Microsoft.JSInterop;

namespace PSPad.App.State;

public sealed class CardCollapseState(IJSRuntime js)
{
    const string StorageKey = "pspad.collapsed";

    readonly HashSet<Guid> _collapsed = [];

    public event Action? Changed;

    public async Task LoadAsync()
    {
        _collapsed.Clear();

        foreach (var part in (await ReadAsync() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(part, out var id))
            {
                _collapsed.Add(id);
            }
        }
    }

    public bool IsCollapsed(Guid listId) => _collapsed.Contains(listId);

    public async Task ToggleAsync(Guid listId)
    {
        if (!_collapsed.Remove(listId))
        {
            _collapsed.Add(listId);
        }

        await js.InvokeAsync<string>("localStorage.setItem", StorageKey, string.Join(',', _collapsed));
        Changed?.Invoke();
    }

    async Task<string?> ReadAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
