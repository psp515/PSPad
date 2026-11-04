using PSPad.Module.Investments.Operations;

namespace PSPad.App.State.Investments;

public sealed record OperationDay(DateOnly Day, IReadOnlyList<InvestmentOperation> Operations);
