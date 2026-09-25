using System.Text.Json;
using Microsoft.JSInterop;
using PSPad.Contracts;

namespace PSPad.App.Statistics;

public sealed class StatisticsCache(IJSRuntime js)
{
    static readonly int[] KnownWindows = [30, 90, 365];

    public async Task<StatisticsOverview?> ReadAsync(int days)
    {
        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("localStorage.getItem", Key(days));
        }
        catch (JSException)
        {
            return null;
        }

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StatisticsOverview>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task WriteAsync(int days, StatisticsOverview overview)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key(days), JsonSerializer.Serialize(overview));
        }
        catch (JSException)
        {
        }
    }

    public async Task ClearAsync()
    {
        foreach (var key in KnownWindows.SelectMany(Keys))
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

    // The key carries the payload shape: a cached overview from an older shape would deserialize
    // with its newest lists missing, and the screen would render null where it expects a series.
    static string Key(int days) => $"pspad.statistics.2.{days}";

    static IEnumerable<string> Keys(int days) => [Key(days), $"pspad.statistics.{days}"];
}
