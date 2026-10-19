using Microsoft.JSInterop;

namespace PSPad.App.Tests.Localization;

sealed class FakeStorageJsRuntime : IJSRuntime
{
    public Dictionary<string, string> Storage { get; } = [];

    public string? BrowserLanguage { get; set; }

    public bool StorageWritesFail { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        switch (identifier)
        {
            case "pspadLanguage":
                return ValueTask.FromResult((TValue)(object)BrowserLanguage!);
            case "localStorage.setItem":
                if (StorageWritesFail)
                {
                    throw new JSException("QuotaExceededError");
                }

                Storage[(string)args![0]!] = (string)args[1]!;
                return ValueTask.FromResult(default(TValue)!);
            case "localStorage.removeItem":
                Storage.Remove((string)args![0]!);
                return ValueTask.FromResult(default(TValue)!);
            case "localStorage.getItem":
                return ValueTask.FromResult((TValue)(object)Storage.GetValueOrDefault((string)args![0]!)!);
            default:
                return ValueTask.FromResult(default(TValue)!);
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}
