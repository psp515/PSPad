using PSPad.App.Sync;

namespace PSPad.App.Api;

public interface IBudgetSharingApi
{
    Task<JoinOutcome> JoinBudgetAsync(string token, string code);

    Task<string?> ShareBudgetAsync(Guid budgetId, string token, string code);

    Task<string?> StopSharingBudgetAsync(Guid budgetId);

    Task<string?> RemoveBudgetMemberAsync(Guid budgetId, Guid memberId);

    Task<string?> LeaveBudgetAsync(Guid budgetId);
}
