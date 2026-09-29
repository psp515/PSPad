using Microsoft.JSInterop;

namespace PSPad.App.State;

public sealed class LastArea(IJSRuntime js)
{
    const string StorageKey = "pspad.last-area";

    public async Task<Guid?> ReadAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return Guid.TryParse(stored, out var areaId) ? areaId : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task RememberAsync(Guid areaId)
    {
        try
        {
            await js.InvokeAsync<string?>("localStorage.setItem", StorageKey, areaId.ToString());
        }
        catch (JSException)
        {
        }
    }
}
