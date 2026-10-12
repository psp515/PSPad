using PSPad.Module.Money.Balance;

namespace PSPad.Module.Money.Reading;

public sealed record HoldingTypeTotal(HoldingType Type, decimal Pln, IReadOnlyList<Holding> Holdings);
