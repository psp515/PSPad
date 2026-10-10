using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record DeleteEntry(Guid CommandId, Guid UserId, Guid EntryId) : ICommand;
