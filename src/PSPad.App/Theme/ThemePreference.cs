using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;

namespace PSPad.App.Theme;

public sealed class ThemePreference(IJSRuntime js)
{
    const string StorageKey = "pspad.theme";
    const string AccentKey = "pspad.accent";
    const string CustomColorKey = "pspad.accent.custom";

    bool _systemPrefersDark;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public Accent Accent { get; private set; } = Accent.Green;

    public string? CustomColor { get; private set; }

    public MudTheme Theme { get; private set; } = PSPadTheme.For(Accent.Green);

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
        var storedColor = await ReadAsync(CustomColorKey);
        CustomColor = MudColor.TryParse(storedColor, out _) ? storedColor : null;
        var accent = Enum.TryParse<Accent>(await ReadAsync(AccentKey), out var stored) ? stored : Accent.Green;
        Apply(accent == Accent.Custom && CustomColor is null ? Accent.Green : accent);

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
        if (accent == Accent.Custom && CustomColor is null)
        {
            return;
        }

        Apply(accent);

        await js.InvokeAsync<string>("localStorage.setItem", AccentKey, Accent.ToString());
        Changed?.Invoke();
    }

    public async Task SetCustomAccentAsync(string color)
    {
        CustomColor = color;
        Apply(Accent.Custom);

        await js.InvokeAsync<string>("localStorage.setItem", CustomColorKey, color);
        await js.InvokeAsync<string>("localStorage.setItem", AccentKey, Accent.ToString());
        Changed?.Invoke();
    }

    void Apply(Accent accent)
    {
        Accent = accent;
        Theme = accent == Accent.Custom ? PSPadTheme.ForCustom(CustomColor!) : PSPadTheme.For(accent);
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
