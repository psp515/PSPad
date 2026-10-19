using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Module.Money.Budgets;

namespace PSPad.App.Tests;

public sealed class FakeBudgetSharingApi(IReplica replica, AppState state, IClock clock) : IBudgetSharingApi
{
    public List<string> Calls { get; } = [];

    public JoinOutcome JoinOutcome { get; set; } = new JoinOutcome.Invalid();

    public (string Token, string Code)? JoinedWith { get; private set; }

    public Task<JoinOutcome> JoinBudgetAsync(string token, string code)
    {
        JoinedWith = (token, code);
        return Task.FromResult(JoinOutcome);
    }

    public Task<SharingRejection?> ShareBudgetAsync(Guid budgetId, string token, string code) =>
        ApplyAsync($"share {budgetId}", budgetId,
            new ShareBudget(Guid.NewGuid(), state.UserId, budgetId, token, code, state.DisplayName));

    public Task<SharingRejection?> StopSharingBudgetAsync(Guid budgetId) =>
        ApplyAsync($"stop {budgetId}", budgetId, new StopSharingBudget(Guid.NewGuid(), state.UserId, budgetId));

    public Task<SharingRejection?> RemoveBudgetMemberAsync(Guid budgetId, Guid memberId) =>
        ApplyAsync($"remove {memberId}", budgetId, new RemoveBudgetMember(Guid.NewGuid(), state.UserId, budgetId, memberId));

    public Task<SharingRejection?> LeaveBudgetAsync(Guid budgetId) =>
        ApplyAsync($"leave {budgetId}", budgetId, new LeaveBudget(Guid.NewGuid(), state.UserId, budgetId));

    async Task<SharingRejection?> ApplyAsync(string call, Guid budgetId, ICommand command)
    {
        Calls.Add(call);
        var budget = await replica.LoadAsync<Budget>(budgetId);

        try
        {
            budget!.ApplyAll(Budget.Decide(budget, command, clock.UtcNow));
            await replica.SaveAsync(budget);
            return null;
        }
        catch (DomainRejectedException rejection)
        {
            return new SharingRejection(SharingFailure.Server, rejection.Message);
        }
    }
}
