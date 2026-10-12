namespace PSPad.Module.Money.Budgets;

public sealed record BudgetMember(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);
