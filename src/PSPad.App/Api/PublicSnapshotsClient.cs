using System.Net.Http.Json;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class PublicSnapshotsClient(HttpClient http)
{
    public async Task<SnapshotView?> GetAsync(string token)
    {
        var response = await http.GetAsync($"api/public/snapshots/{token}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SnapshotView>()
            : null;
    }

    public async Task<bool> MarkAsync(string token, Guid entryId, Guid? stepId, bool marked)
    {
        var response = await http.PostAsJsonAsync(
            $"api/public/snapshots/{token}/marks", new MarkSnapshotEntryRequest(entryId, stepId, marked));
        return response.IsSuccessStatusCode;
    }
}
