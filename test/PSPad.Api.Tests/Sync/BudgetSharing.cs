using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.Module.Money.Budgets;

namespace PSPad.Api.Tests.Sync;

public static class BudgetSharing
{
    public static async Task<Guid> SharedBudgetAsync(HttpClient owner, Guid ownerId, CancellationToken ct, string token)
    {
        var budgetId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateBudget(Guid.NewGuid(), ownerId, budgetId, "Household"));
        (await ShareAsync(owner, budgetId, token, ct)).EnsureSuccessStatusCode();
        return budgetId;
    }

    public static Task<HttpResponseMessage> JoinAsync(
        HttpClient member, CancellationToken ct, string token, string? code = null) =>
        member.PostAsJsonAsync("/api/budgets/join", new JoinBudgetRequest(token, code ?? Sharing.Code), ct);

    public static Task<HttpResponseMessage> ShareAsync(
        HttpClient owner, Guid budgetId, string token, CancellationToken ct, string? code = null) =>
        owner.PostAsJsonAsync($"/api/budgets/{budgetId}/share", new ShareBudgetRequest(token, code ?? Sharing.Code), ct);

    public static Task<HttpResponseMessage> StopSharingAsync(HttpClient owner, Guid budgetId, CancellationToken ct) =>
        owner.DeleteAsync($"/api/budgets/{budgetId}/share", ct);

    public static Task<HttpResponseMessage> RemoveMemberAsync(
        HttpClient owner, Guid budgetId, Guid memberId, CancellationToken ct) =>
        owner.DeleteAsync($"/api/budgets/{budgetId}/members/{memberId}", ct);

    public static Task<HttpResponseMessage> LeaveAsync(HttpClient member, Guid budgetId, CancellationToken ct) =>
        member.PostAsync($"/api/budgets/{budgetId}/leave", null, ct);
}
