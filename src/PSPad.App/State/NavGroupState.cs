using Microsoft.JSInterop;

namespace PSPad.App.State;

public sealed class NavGroupState(IJSRuntime js)
{
    public const string Budgets = "budgets";
    public const string Areas = "areas";

    const string StorageKey = "pspad.navgroups";

    readonly HashSet<string> _collapsed = [];

    public event Action? Changed;

    public async Task LoadAsync()
    {
        _collapsed.Clear();
        foreach (var group in (await ReadAsync() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            _collapsed.Add(group);
        }
    }

    public bool IsExpanded(string group) => !_collapsed.Contains(group);

    public async Task SetAsync(string group, bool expanded)
    {
        if (expanded ? !_collapsed.Remove(group) : !_collapsed.Add(group))
        {
            return;
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
