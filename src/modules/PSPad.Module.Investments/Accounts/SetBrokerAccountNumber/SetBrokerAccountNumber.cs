using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record SetBrokerAccountNumber(Guid CommandId, Guid UserId, Guid AccountId, string Number) : ICommand;
