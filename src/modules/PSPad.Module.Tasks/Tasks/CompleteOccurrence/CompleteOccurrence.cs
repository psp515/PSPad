using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record CompleteOccurrence(
    Guid CommandId, Guid UserId, Guid TaskId, DateOnly Day, bool Completed) : ICommand;
