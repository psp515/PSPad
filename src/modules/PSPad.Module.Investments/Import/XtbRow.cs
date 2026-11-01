using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Import;

public sealed record XtbRow(
    int SheetRow, string Type, XtbRowKind? Kind, string? Instrument, string? Ticker, DateTimeOffset? At,
    decimal? Amount, string? Id, string? Comment, string? Product, string? PositionId, decimal? Quantity, decimal? Price,
    IReadOnlyList<string> Errors, TransferType? TransferType = null, string? Counterparty = null,
    decimal? CounterAmount = null, string? CounterCurrency = null);
