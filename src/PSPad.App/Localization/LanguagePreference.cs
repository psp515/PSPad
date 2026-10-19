using System.Globalization;
using Microsoft.JSInterop;
using PSPad.Abstractions;

namespace PSPad.App.Localization;

public sealed class LanguagePreference(IJSRuntime js)
{
    const string StorageKey = "pspad.language";
    const string PendingKey = "pspad.language.pending";

    public string Language { get; private set; } = SupportedLanguages.Default;

    public bool HasPendingWrite { get; private set; }

    public bool HadCachedChoice { get; private set; }

    public event Action? Changed;

    public async Task InitialiseAsync()
    {
        var stored = await ReadAsync(StorageKey);
        HadCachedChoice = SupportedLanguages.IsSupported(stored);
        Language = HadCachedChoice
            ? stored!
            : SupportedLanguages.FromBrowser(await BrowserLanguage.ReadAsync(js));
        HasPendingWrite = await ReadAsync(PendingKey) == "1";
        ApplyCulture(Language);
        Changed?.Invoke();
    }

    public async Task<bool> AdoptFromServerAsync(string language)
    {
        if (HasPendingWrite || !SupportedLanguages.IsSupported(language) || language == Language)
        {
            return false;
        }

        Language = language;
        var persisted = await WriteAsync(StorageKey, language);
        Changed?.Invoke();
        return persisted;
    }

    public async Task ChooseAsync(string language)
    {
        Language = SupportedLanguages.Require(language);
        HasPendingWrite = true;
        await WriteAsync(StorageKey, Language);
        await WriteAsync(PendingKey, "1");
        Changed?.Invoke();
    }

    public async Task ClearPendingAsync()
    {
        HasPendingWrite = false;
        await RemoveAsync(PendingKey);
    }

    public static void ApplyCulture(string language)
    {
        // en-GB, not en-US: the app renders day-first dates and 24-hour times.
        var culture = CultureInfo.GetCultureInfo(language == "pl" ? "pl-PL" : "en-GB");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
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

    async Task<bool> WriteAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }

    async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
        }
    }
}
