using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Api.Tests.Sync;

public static class Sharing
{
    public static Task<Guid> SignInAsync(HttpClient client, CancellationToken ct) =>
        Commands.Commands.SignInAsync(client, ct);

    public static Task SendAsync(HttpClient client, CancellationToken ct, params object[] commands) =>
        Commands.Commands.SendAsync(client, ct, commands);

    public static async Task<HttpResponseMessage> JoinAsync(
        HttpClient member, CancellationToken ct, string token, string? code = null) =>
        await member.PostAsJsonAsync("/api/lists/join", new JoinListRequest(token, code ?? Code), ct);

    public static async Task<Guid> SharedListAsync(
        HttpClient owner, Guid ownerId, CancellationToken ct, ListKind kind = ListKind.Tasks, string? token = null)
    {
        token ??= FreshToken();
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Książki", kind));
        (await ShareAsync(owner, listId, token, ct)).EnsureSuccessStatusCode();
        return listId;
    }

    public static Task<HttpResponseMessage> ShareAsync(
        HttpClient owner, Guid listId, string token, CancellationToken ct, string? code = null) =>
        owner.PostAsJsonAsync($"/api/lists/{listId}/share", new ShareListRequest(token, code ?? Code), ct);

    public static Task<HttpResponseMessage> StopSharingAsync(HttpClient owner, Guid listId, CancellationToken ct) =>
        owner.DeleteAsync($"/api/lists/{listId}/share", ct);

    public static Task<HttpResponseMessage> RemoveMemberAsync(
        HttpClient owner, Guid listId, Guid memberId, CancellationToken ct) =>
        owner.DeleteAsync($"/api/lists/{listId}/members/{memberId}", ct);

    public static Task<HttpResponseMessage> LeaveAsync(HttpClient member, Guid listId, CancellationToken ct) =>
        member.PostAsync($"/api/lists/{listId}/leave", null, ct);

    public const string Code = "K7M4PX";

    public static string FreshToken() => Guid.NewGuid().ToString("N");
}
