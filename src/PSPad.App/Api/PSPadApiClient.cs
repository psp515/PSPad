using System.Net;
using System.Net.Http.Json;
using PSPad.App.Statistics;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class PSPadApiClient(HttpClient http) : IStatisticsSource, ISyncApi, ISnapshotsApi
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
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<DeleteAccountResponse?> DeleteAccountAsync()
    {
        try
        {
            var response = await http.DeleteAsync("api/account");

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<DeleteAccountResponse>()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
    {
        var response = await http.PostAsJsonAsync("api/commands", envelopes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
    }

    public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
        GetAsync<SyncResponse>(full.Count == 0
            ? $"api/sync?since={since}"
            : $"api/sync?since={since}&full={string.Join(',', full)}");

    public async Task<JoinListResponse?> JoinAsync(string token)
    {
        var response = await http.PostAsJsonAsync("api/lists/join", new JoinListRequest(token));
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JoinListResponse>();
    }

    public Task<StatisticsOverview?> OverviewAsync(int days) =>
        GetAsync<StatisticsOverview>($"api/statistics/overview?days={days}");

    public async Task<IReadOnlyList<StatisticsRecordView>> RecordsAsync(long? before, int limit)
    {
        var query = before is null
            ? $"api/statistics/records?limit={limit}"
            : $"api/statistics/records?limit={limit}&before={before}";
        return await GetAsync<StatisticsRecordView[]>(query) ?? [];
    }

    public async Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt)
    {
        var response = await http.PostAsJsonAsync($"api/lists/{listId}/snapshots", new PublishSnapshotRequest(expiresAt));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PublishedSnapshotView>()
            : null;
    }

    public async Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId) =>
        await GetAsync<PublishedSnapshotView[]>($"api/lists/{listId}/snapshots") ?? [];

    public async Task<bool> RevokeAsync(Guid snapshotId)
    {
        var response = await http.DeleteAsync($"api/snapshots/{snapshotId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RecordVisitAsync(string token)
    {
        var response = await http.PostAsJsonAsync("api/me/snapshot-visits", new RecordVisitRequest(token));
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
        await GetAsync<SnapshotVisitView[]>("api/me/snapshot-visits") ?? [];

    Task<T?> GetAsync<T>(string uri) => http.GetFromJsonAsync<T>(uri);
}
