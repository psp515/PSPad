using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record CreateInvestmentAccount(Guid CommandId, Guid UserId, Guid AccountId, string Name, string Currency, Broker Broker, string? BrokerAccountNumber) : ICommand;
