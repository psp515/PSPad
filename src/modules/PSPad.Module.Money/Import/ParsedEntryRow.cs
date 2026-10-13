using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Import;

public sealed record ParsedEntryRow(
    int Line, CategoryKind Kind, DateOnly Date, string Name, string Category, decimal Amount, string? Currency,
    decimal? Rate, string? Note);
