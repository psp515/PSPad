using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed record EnsureInstrument(Guid CommandId, Guid UserId, string Ticker, string Name) : ICommand;
