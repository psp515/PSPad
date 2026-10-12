using PSPad.Module.Money.Budgets;

namespace PSPad.App.State;

public static class BudgetOrder
{
    public static IReadOnlyList<Budget> For(IEnumerable<Budget> budgets, Guid me)
    {
        var visible = budgets.Where(budget => budget.Admits(me)).ToArray();
        var mine = visible.Where(budget => budget.UserId == me).OrderBy(budget => budget.CreatedAt);
        var shared = visible.Where(budget => budget.UserId != me).OrderBy(budget => JoinedAt(budget, me));
        return [.. mine, .. shared];
    }

    public static bool IsSharedWith(Budget budget, Guid me) => budget.UserId != me && budget.HasMember(me);

    static DateTimeOffset JoinedAt(Budget budget, Guid me) =>
        budget.Members.First(member => member.UserId == me).JoinedAt;
}
