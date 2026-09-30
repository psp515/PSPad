using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed class TaskList : Aggregate
{
    [JsonInclude]
    public Guid AreaId { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public DateTimeOffset? CreatedAt { get; private set; }

    [JsonInclude]
    public ListKind Kind { get; private set; } = ListKind.Tasks;

    public static IReadOnlyList<DomainEvent> Decide(TaskList? list, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateTaskList create:
                if (list is not null)
                {
                    throw new DomainRejectedException("That list already exists.");
                }

                if (create.AreaId == Guid.Empty)
                {
                    throw new DomainRejectedException("A list has to live in an area.");
                }

                return [new TaskListCreated(
                    create.ListId,
                    create.UserId,
                    at,
                    create.AreaId,
                    RequireName(create.Name),
                    create.Kind)];

            case RenameTaskList rename:
                var renaming = Require(list, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new TaskListRenamed(renaming.Id, rename.UserId, at, name)];

            case MoveTaskListToArea move:
                var moving = Require(list, move.UserId);
                if (move.AreaId == Guid.Empty)
                {
                    throw new DomainRejectedException("A list has to live in an area.");
                }

                return moving.AreaId == move.AreaId
                    ? []
                    : [new TaskListMovedToArea(moving.Id, move.UserId, at, move.AreaId)];

            case DeleteTaskList delete:
                var deleting = Require(list, delete.UserId);
                return deleting.Deleted ? [] : [new TaskListDeleted(deleting.Id, delete.UserId, at)];

            default:
                throw new DomainRejectedException($"A list cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case TaskListCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                AreaId = created.AreaId;
                Name = created.Name;
                CreatedAt = created.At;
                Kind = created.Kind;
                break;
            case TaskListRenamed renamed:
                Name = renamed.Name;
                break;
            case TaskListMovedToArea moved:
                AreaId = moved.AreaId;
                break;
            case TaskListDeleted:
                Deleted = true;
                break;
        }
    }

    internal static TaskList Require(TaskList? list, Guid userId)
    {
        if (list is null || list.Deleted)
        {
            throw new DomainRejectedException("That list no longer exists.");
        }

        if (list.UserId != userId)
        {
            throw new DomainRejectedException("That list belongs to somebody else.");
        }

        return list;
    }

    internal static TaskList RequireAcceptsTasks(TaskList? list, Guid userId)
    {
        var existing = Require(list, userId);
        return existing.Kind == ListKind.Reference
            ? throw new DomainRejectedException("That list holds references, not tasks.")
            : existing;
    }

    internal static TaskList RequireAcceptsReferences(TaskList? list, Guid userId)
    {
        var existing = Require(list, userId);
        return existing.Kind == ListKind.Tasks
            ? throw new DomainRejectedException("That list holds tasks, not references.")
            : existing;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A list needs a name.")
            : name.Trim();
}
