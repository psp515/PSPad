using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Api.Tests.Sync;

public static class Sharing
{
    public static async Task<Guid> SignInAsync(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

    public static async Task SendAsync(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }

    public static async Task<HttpResponseMessage> JoinAsync(
        HttpClient member, CancellationToken ct, string token) =>
        await member.PostAsJsonAsync("/api/lists/join", new JoinListRequest(token), ct);

    public static async Task<Guid> SharedListAsync(
        HttpClient owner, Guid ownerId, CancellationToken ct, ListKind kind = ListKind.Tasks, string? token = null)
    {
        token ??= FreshToken();
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Książki", kind),
            new ShareTaskList(Guid.NewGuid(), ownerId, listId, token, "Owner"));
        return listId;
    }

    public static string FreshToken() => Guid.NewGuid().ToString("N");
}
