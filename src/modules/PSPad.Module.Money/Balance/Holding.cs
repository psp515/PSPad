namespace PSPad.Module.Money.Balance;

public sealed record Holding(Guid HoldingId, string Name, HoldingType Type, Money Money, Guid? InvestmentAccountId = null);
