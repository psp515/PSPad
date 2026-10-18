using Microsoft.JSInterop;

namespace PSPad.App.Localization;

public static class BrowserLanguage
{
    public static async Task<string?> ReadAsync(IJSRuntime js)
    {
        try
        {
            return await js.InvokeAsync<string?>("pspadLanguage");
        }
        catch (JSException)
        {
            return null;
        }
    }
}
