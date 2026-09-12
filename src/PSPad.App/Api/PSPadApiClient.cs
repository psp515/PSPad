using System.Net.Http.Json;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class PSPadApiClient(HttpClient http)
{
    public async Task<MeResponse?> MeAsync() =>
        await http.GetFromJsonAsync<MeResponse>("api/me");

    public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
    {
        var response = await http.PostAsJsonAsync("api/commands", envelopes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
    }

    public async Task<SyncResponse?> SyncAsync(long since) =>
        await http.GetFromJsonAsync<SyncResponse>($"api/sync?since={since}");

    public async Task<IReadOnlyList<HistoryEntry>> HistoryAsync(long? before, int limit)
    {
        var query = before is null ? $"api/history?limit={limit}" : $"api/history?limit={limit}&before={before}";
        return await http.GetFromJsonAsync<HistoryEntry[]>(query) ?? [];
    }
}
