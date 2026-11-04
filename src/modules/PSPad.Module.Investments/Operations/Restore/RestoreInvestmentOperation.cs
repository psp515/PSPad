using PSPad.Abstractions;

namespace PSPad.Module.Investments.Operations;

public sealed record RestoreInvestmentOperation(Guid CommandId, Guid UserId, Guid OperationId) : ICommand;
