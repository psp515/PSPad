using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Statistics;
using PSPad.App.Sync;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class PSPadApiClient(HttpClient http) : IStatisticsSource, ISyncApi, ILanguageApi,ISnapshotsApi, INbpRates, IListSharingApi,
    IBudgetSharingApi
{
    static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

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

    public async Task<MeResponse?> SetLanguageAsync(string language)
    {
        try
        {
            var response = await http.PutAsJsonAsync(
                "api/me/language", new SetLanguageRequest(language));

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<MeResponse>()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<AccountDeletionOutcome> DeleteAccountAsync()
    {
        try
        {
            var response = await http.DeleteAsync("api/account");

            if (StepUpChallenge.Names(response))
            {
                return new AccountDeletionOutcome.ReauthenticationRequired();
            }

            if (!response.IsSuccessStatusCode)
            {
                return new AccountDeletionOutcome.Failed();
            }

            var deleted = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>();
            return deleted is null
                ? new AccountDeletionOutcome.Failed()
                : new AccountDeletionOutcome.Deleted(deleted.KeycloakRemoved);
        }
        catch (HttpRequestException)
        {
            return new AccountDeletionOutcome.Failed();
        }
    }

    public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
    {
        var response = await http.PostAsJsonAsync("api/commands", envelopes);
        ThrowIfBusy(response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
    }

    public async Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
    {
        var response = await http.GetAsync(full.Count == 0
            ? $"api/sync?since={since}"
            : $"api/sync?since={since}&full={string.Join(',', full)}");
        ThrowIfBusy(response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncResponse>();
    }

    static void ThrowIfBusy(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return;
        }

        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;

        throw new ServerBusyException(retryAfter > TimeSpan.Zero ? retryAfter.Value : DefaultRetryAfter);
    }

    public async Task<JoinOutcome> JoinAsync(string token, string code)
    {
        var response = await http.PostAsJsonAsync("api/lists/join", new JoinListRequest(token, code));

        if (FailedJoin(response) is { } failed)
        {
            return failed;
        }

        var joined = await response.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JoinListResponse>();
        return new JoinOutcome.Joined(joined!.ListId, joined.Documents);
    }

    public async Task<JoinOutcome> JoinBudgetAsync(string token, string code)
    {
        var response = await http.PostAsJsonAsync("api/budgets/join", new JoinBudgetRequest(token, code));

        if (FailedJoin(response) is { } failed)
        {
            return failed;
        }

        var joined = await response.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JoinBudgetResponse>();
        return new JoinOutcome.Joined(joined!.BudgetId, joined.Documents);
    }

    static JoinOutcome? FailedJoin(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.NotFound => new JoinOutcome.Invalid(),
        HttpStatusCode.Gone => new JoinOutcome.Expired(),
        HttpStatusCode.TooManyRequests => new JoinOutcome.TooManyTries(),
        _ => null
    };

    public Task<SharingRejection?> ShareListAsync(Guid listId, string token, string code) =>
        ChangeSharingAsync(() => http.PostAsJsonAsync($"api/lists/{listId}/share", new ShareListRequest(token, code)), SharingFailure.ListGone);

    public Task<SharingRejection?> StopSharingAsync(Guid listId) =>
        ChangeSharingAsync(() => http.DeleteAsync($"api/lists/{listId}/share"), SharingFailure.ListGone);

    public Task<SharingRejection?> RemoveMemberAsync(Guid listId, Guid memberId) =>
        ChangeSharingAsync(() => http.DeleteAsync($"api/lists/{listId}/members/{memberId}"), SharingFailure.ListGone);

    public Task<SharingRejection?> LeaveListAsync(Guid listId) =>
        ChangeSharingAsync(() => http.PostAsync($"api/lists/{listId}/leave", null), SharingFailure.ListGone);

    public Task<SharingRejection?> ShareBudgetAsync(Guid budgetId, string token, string code) =>
        ChangeSharingAsync(
            () => http.PostAsJsonAsync($"api/budgets/{budgetId}/share", new ShareBudgetRequest(token, code)), SharingFailure.BudgetGone);

    public Task<SharingRejection?> StopSharingBudgetAsync(Guid budgetId) =>
        ChangeSharingAsync(() => http.DeleteAsync($"api/budgets/{budgetId}/share"), SharingFailure.BudgetGone);

    public Task<SharingRejection?> RemoveBudgetMemberAsync(Guid budgetId, Guid memberId) =>
        ChangeSharingAsync(() => http.DeleteAsync($"api/budgets/{budgetId}/members/{memberId}"), SharingFailure.BudgetGone);

    public Task<SharingRejection?> LeaveBudgetAsync(Guid budgetId) =>
        ChangeSharingAsync(() => http.PostAsync($"api/budgets/{budgetId}/leave", null), SharingFailure.BudgetGone);

    static async Task<SharingRejection?> ChangeSharingAsync(Func<Task<HttpResponseMessage>> send, SharingFailure gone)
    {
        try
        {
            var response = await send();

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Conflict &&
                await response.Content.ReadFromJsonAsync<JsonElement>() is { ValueKind: JsonValueKind.Object } problem &&
                problem.TryGetProperty("detail", out var detail) && detail.GetString() is { } reason)
            {
                return new SharingRejection(SharingFailure.Server, reason);
            }

            return new SharingRejection(response.StatusCode == HttpStatusCode.NotFound ? gone : SharingFailure.Failed);
        }
        catch (HttpRequestException)
        {
            return new SharingRejection(SharingFailure.Offline);
        }
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

    public async Task<NbpRateView?> RateAsync(string currency, DateOnly date)
    {
        try
        {
            var response = await http.GetAsync(
                $"api/money/nbp-rate?currency={Uri.EscapeDataString(currency)}&date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<NbpRateView>() : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    Task<T?> GetAsync<T>(string uri) => http.GetFromJsonAsync<T>(uri);
}
