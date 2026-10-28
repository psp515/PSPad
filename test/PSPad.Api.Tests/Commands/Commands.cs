using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;

namespace PSPad.Api.Tests.Commands;

public static class Commands
{
    public static async Task<Guid> SignInAsync(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

    public static async Task<CommandResponse[]> TrySendAsync(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
    }

    public static async Task SendAsync(HttpClient client, CancellationToken ct, params object[] commands) =>
        Assert.All(await TrySendAsync(client, ct, commands), result => Assert.True(result.Accepted, result.Rejection));

    public static async Task<IReadOnlyList<JsonElement>> SyncedAsync(HttpClient client, string collection, CancellationToken ct)
    {
        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        return sync!.Documents.TryGetValue(collection, out var documents) ? documents : [];
    }
}
