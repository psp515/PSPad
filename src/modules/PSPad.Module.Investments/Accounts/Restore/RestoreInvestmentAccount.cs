using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record RestoreInvestmentAccount(Guid CommandId, Guid UserId, Guid AccountId) : ICommand;
