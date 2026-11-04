using PSPad.Abstractions;

namespace PSPad.Module.Investments.Operations;

public sealed record DeleteInvestmentOperation(Guid CommandId, Guid UserId, Guid OperationId) : ICommand;
