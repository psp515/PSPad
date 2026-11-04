using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record RenameInvestmentAccount(Guid CommandId, Guid UserId, Guid AccountId, string Name) : ICommand;
