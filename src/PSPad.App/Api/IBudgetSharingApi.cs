using PSPad.App.Sync;

namespace PSPad.App.Api;

public interface IBudgetSharingApi
{
    Task<JoinOutcome> JoinBudgetAsync(string token, string code);

    Task<SharingRejection?> ShareBudgetAsync(Guid budgetId, string token, string code);

    Task<SharingRejection?> StopSharingBudgetAsync(Guid budgetId);

    Task<SharingRejection?> RemoveBudgetMemberAsync(Guid budgetId, Guid memberId);

    Task<SharingRejection?> LeaveBudgetAsync(Guid budgetId);
}
