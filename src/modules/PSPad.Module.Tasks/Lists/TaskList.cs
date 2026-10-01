using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed class TaskList : Aggregate
{
    const int ShortestToken = 22;

    [JsonInclude]
    public Guid AreaId { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public DateTimeOffset? CreatedAt { get; private set; }

    [JsonInclude]
    public ListKind Kind { get; private set; } = ListKind.Tasks;

    [JsonInclude]
    public string? InviteToken { get; private set; }

    [JsonInclude]
    public string? OwnerName { get; private set; }

    [JsonInclude]
    List<ListMember> _members = [];

    public IReadOnlyList<ListMember> Members => _members;

    public bool IsShared => _members.Count > 0;

    public bool HasMember(Guid userId) => _members.Any(member => member.UserId == userId);

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
                return renaming.Name == name ? [] : [new TaskListRenamed(renaming.Id, renaming.UserId, at, name)];

            case MoveTaskListToArea move:
                var moving = Require(list, move.UserId);
                if (move.AreaId == Guid.Empty)
                {
                    throw new DomainRejectedException("A list has to live in an area.");
                }

                return moving.AreaId == move.AreaId
                    ? []
                    : [new TaskListMovedToArea(moving.Id, moving.UserId, at, move.AreaId)];

            case DeleteTaskList delete:
                var deleting = Require(list, delete.UserId);
                return deleting.Deleted ? [] : [new TaskListDeleted(deleting.Id, deleting.UserId, at)];

            case ShareTaskList share:
                var sharing = Require(list, share.UserId);
                var token = share.Token;
                if (string.IsNullOrWhiteSpace(token) || token.Length < ShortestToken)
                {
                    throw new DomainRejectedException("An invite link needs a longer token.");
                }

                var ownerName = share.OwnerName.Trim();
                return sharing.InviteToken == token && sharing.OwnerName == ownerName
                    ? []
                    : [new TaskListShared(sharing.Id, sharing.UserId, at, token, ownerName)];

            case StopSharingTaskList stop:
                var stopping = Require(list, stop.UserId);
                return stopping.InviteToken is null ? [] : [new TaskListSharingStopped(stopping.Id, stopping.UserId, at)];

            case RemoveListMember remove:
                var removing = Require(list, remove.UserId);
                return removing.HasMember(remove.MemberId)
                    ? [new ListMemberRemoved(removing.Id, removing.UserId, at, remove.MemberId)]
                    : throw new DomainRejectedException("That person is not a member of this list.");

            case LeaveTaskList leave:
                var leaving = Live(list);
                return leaving.HasMember(leave.UserId)
                    ? [new TaskListLeft(leaving.Id, leaving.UserId, at, leave.UserId)]
                    : throw new DomainRejectedException(leaving.UserId == leave.UserId
                        ? "The owner cannot leave their own list."
                        : "You are not a member of this list.");

            case JoinTaskList join:
                var joining = Live(list);
                if (joining.InviteToken is null || joining.InviteToken != join.Token)
                {
                    throw new DomainRejectedException("This invite link no longer works.");
                }

                return joining.UserId == join.UserId || joining.HasMember(join.UserId)
                    ? []
                    : [new TaskListJoined(joining.Id, joining.UserId, at, join.UserId, RequireMemberName(join.DisplayName))];

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
            case TaskListShared shared:
                InviteToken = shared.Token;
                OwnerName = shared.OwnerName;
                break;
            case TaskListSharingStopped:
                InviteToken = null;
                break;
            case TaskListJoined joined:
                _members.Add(new ListMember(joined.MemberId, joined.DisplayName, joined.At));
                break;
            case ListMemberRemoved removed:
                _members.RemoveAll(member => member.UserId == removed.MemberId);
                break;
            case TaskListLeft left:
                _members.RemoveAll(member => member.UserId == left.MemberId);
                break;
        }
    }

    static TaskList Live(TaskList? list)
    {
        if (list is null || list.Deleted)
        {
            throw new DomainRejectedException("That list no longer exists.");
        }

        return list;
    }

    internal static TaskList Require(TaskList? list, Guid userId)
    {
        var existing = Live(list);
        if (existing.UserId != userId)
        {
            throw new DomainRejectedException("That list belongs to somebody else.");
        }

        return existing;
    }

    internal static ListAccess RequireAcceptsTasks(TaskList? list, Guid actorId)
    {
        var access = ListAccess.To(list, actorId);
        return list!.Kind == ListKind.Reference
            ? throw new DomainRejectedException("That list holds references, not tasks.")
            : access;
    }

    internal static ListAccess RequireAcceptsReferences(TaskList? list, Guid actorId)
    {
        var access = ListAccess.To(list, actorId);
        return list!.Kind == ListKind.Tasks
            ? throw new DomainRejectedException("That list holds tasks, not references.")
            : access;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A list needs a name.")
            : name.Trim();

    static string RequireMemberName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A member needs a name.")
            : name.Trim();
}
