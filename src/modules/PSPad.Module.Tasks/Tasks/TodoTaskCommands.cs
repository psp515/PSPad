using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record CreateTask(Guid CommandId, Guid UserId, Guid TaskId, Guid ListId, string Name) : ICommand;

public sealed record RenameTask(Guid CommandId, Guid UserId, Guid TaskId, string Name) : ICommand;

public sealed record SetTaskDueDate(Guid CommandId, Guid UserId, Guid TaskId, DateOnly? DueOn) : ICommand;

public sealed record SetTaskPriority(Guid CommandId, Guid UserId, Guid TaskId, Priority Priority) : ICommand;

public sealed record StarTask(Guid CommandId, Guid UserId, Guid TaskId, bool Starred) : ICommand;

public sealed record LinkTaskToGoal(Guid CommandId, Guid UserId, Guid TaskId, Guid? GoalId) : ICommand;

public sealed record MoveTaskToList(Guid CommandId, Guid UserId, Guid TaskId, Guid ListId) : ICommand;

public sealed record CompleteTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;

public sealed record ReopenTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;

public sealed record DeleteTask(Guid CommandId, Guid UserId, Guid TaskId) : ICommand;
