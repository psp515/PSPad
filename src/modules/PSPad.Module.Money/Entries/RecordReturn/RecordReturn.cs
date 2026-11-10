using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record RecordReturn(
    Guid CommandId, Guid UserId, Guid EntryId, Guid PurchaseId, decimal Amount, DateOnly Date, string? Note) : ICommand;
