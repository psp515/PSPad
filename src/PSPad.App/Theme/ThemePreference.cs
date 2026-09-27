using Microsoft.JSInterop;
using MudBlazor;

namespace PSPad.App.Theme;

public sealed class ThemePreference(IJSRuntime js)
{
    const string StorageKey = "pspad.theme";
    const string AccentKey = "pspad.accent";

    bool _systemPrefersDark;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public Accent Accent { get; private set; } = Accent.Green;

    public MudTheme Theme => PSPadTheme.For(Accent);

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

        Mode = Enum.TryParse<ThemeMode>(await ReadAsync(StorageKey), out var mode) ? mode : ThemeMode.System;
        Accent = Enum.TryParse<Accent>(await ReadAsync(AccentKey), out var accent) ? accent : Accent.Green;

        Changed?.Invoke();
    }

    public async Task SetAsync(ThemeMode mode)
    {
        Mode = mode;

        await js.InvokeAsync<string>("localStorage.setItem", StorageKey, Mode.ToString());
        Changed?.Invoke();
    }

    public async Task SetAccentAsync(Accent accent)
    {
        Accent = accent;

        await js.InvokeAsync<string>("localStorage.setItem", AccentKey, Accent.ToString());
        Changed?.Invoke();
    }

    async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
