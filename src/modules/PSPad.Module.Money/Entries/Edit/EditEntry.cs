using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record EditEntry(
    Guid CommandId, Guid UserId, Guid EntryId, string Name, string Category, Money Money, DateOnly Date,
    string? Note) : ICommand;
