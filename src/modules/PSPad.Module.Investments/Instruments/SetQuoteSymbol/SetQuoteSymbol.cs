using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed record SetQuoteSymbol(Guid CommandId, Guid UserId, string Ticker, string Symbol) : ICommand;
