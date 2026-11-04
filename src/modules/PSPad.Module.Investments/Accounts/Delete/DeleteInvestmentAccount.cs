using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record DeleteInvestmentAccount(Guid CommandId, Guid UserId, Guid AccountId) : ICommand;
