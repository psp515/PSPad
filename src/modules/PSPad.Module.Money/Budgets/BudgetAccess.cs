using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public static class BudgetAccess
{
    public static Budget To(Budget? budget, Guid actorId) =>
        budget is null || (budget.UserId != actorId && !budget.HasMember(actorId))
            ? throw Missing()
            : budget;

    public static Budget Owned(Budget? budget, Guid actorId) =>
        budget is null || budget.UserId != actorId ? throw Missing() : budget;

    public static Budget Writable(Budget budget) =>
        budget.IsArchived ? throw new DomainRejectedException("This budget is archived.") : budget;

    static DomainRejectedException Missing() => new("That budget does not exist.");
}
