using PSPad.Module.Money.Balance;

namespace PSPad.Module.Money.Import;

public sealed record HoldingChange(HoldingChangeKind Kind, Holding Holding, int? Line);
