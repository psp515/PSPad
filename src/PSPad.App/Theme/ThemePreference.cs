using Microsoft.JSInterop;

namespace PSPad.App.Theme;

public sealed class ThemePreference(IJSRuntime js)
{
    const string StorageKey = "pspad.theme";

    bool _systemPrefersDark;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public bool IsDark => Mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => _systemPrefersDark
    };

    public event Action? Changed;

    public async Task InitialiseAsync(bool systemPrefersDark)
    {
        _systemPrefersDark = systemPrefersDark;

        var stored = await ReadAsync();
        Mode = Enum.TryParse<ThemeMode>(stored, out var parsed) ? parsed : ThemeMode.System;

        Changed?.Invoke();
    }

    public async Task SetAsync(ThemeMode mode)
    {
        Mode = mode;

        await js.InvokeAsync<string>("localStorage.setItem", StorageKey, Mode.ToString());
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
