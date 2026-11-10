using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record EditReturn(Guid CommandId, Guid UserId, Guid EntryId, decimal Amount, DateOnly Date, string? Note) : ICommand;
