using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class PSPadApiClient(HttpClient http) : IHistorySource, ISyncApi
{
    public Task<MeResponse?> MeAsync() => GetAsync<MeResponse>("api/me");

    public async Task<MeResponse?> SetTimeZoneAsync(string timeZone)
    {
        try
        {
            var response = await http.PutAsJsonAsync(
                "api/me/timezone", new SetTimeZoneRequest(timeZone));

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<MeResponse>()
                : null;
        }
        catch (AccessTokenNotAvailableException expired)
        {
            expired.Redirect();
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/commands", envelopes);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
        }
        catch (AccessTokenNotAvailableException expired)
        {
            expired.Redirect();
            return [];
        }
    }

    public Task<SyncResponse?> SyncAsync(long since) => GetAsync<SyncResponse>($"api/sync?since={since}");

    public async Task<IReadOnlyList<HistoryEntry>> HistoryAsync(long? before, int limit)
    {
        var query = before is null ? $"api/history?limit={limit}" : $"api/history?limit={limit}&before={before}";
        return await GetAsync<HistoryEntry[]>(query) ?? [];
    }

    Task<IReadOnlyList<HistoryEntry>> IHistorySource.ReadAsync(long? before, int limit) =>
        HistoryAsync(before, limit);

    async Task<T?> GetAsync<T>(string uri)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(uri);
        }
        catch (AccessTokenNotAvailableException expired)
        {
            expired.Redirect();
            return default;
        }
    }
}
