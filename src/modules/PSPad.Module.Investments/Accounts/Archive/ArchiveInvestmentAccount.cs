using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record ArchiveInvestmentAccount(Guid CommandId, Guid UserId, Guid AccountId) : ICommand;
