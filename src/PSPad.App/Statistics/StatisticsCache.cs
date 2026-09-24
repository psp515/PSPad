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
        foreach (var days in KnownWindows)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.removeItem", Key(days));
            }
            catch (JSException)
            {
            }
        }
    }

    static string Key(int days) => $"pspad.statistics.{days}";
}
