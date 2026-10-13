using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed class TaskList : Aggregate
{
    public const int MostWrongCodes = Invites.MostWrongCodes;

    public static readonly TimeSpan InviteLife = Invites.Life;

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
    public string? InviteCode { get; private set; }

    [JsonInclude]
    public DateTimeOffset? InviteExpiresAt { get; private set; }

    [JsonInclude]
    public int WrongCodes { get; private set; }

    [JsonInclude]
    public string? OwnerName { get; private set; }

    [JsonInclude]
    List<ListMember> _members = [];

    public IReadOnlyList<ListMember> Members => _members;

    public bool ClosedByWrongCodes => InviteToken is null && WrongCodes >= MostWrongCodes;

    public bool IsInviteLiveAt(DateTimeOffset now) => Invites.IsLiveAt(InviteToken, InviteCode, InviteExpiresAt, now);

    public InviteCheck CheckInvite(string token, string code, DateTimeOffset now) =>
        Invites.Check(InviteToken, InviteCode, InviteExpiresAt, token, code, now);

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
                var token = Invites.RequireToken(share.Token);
                var code = Invites.RequireCode(share.Code);

                var ownerName = ContentLimits.Cut(share.OwnerName.Trim(), ContentLimits.Name);
                return sharing.InviteToken == token && sharing.InviteCode == code && sharing.OwnerName == ownerName
                    && sharing.IsInviteLiveAt(at)
                    ? []
                    : [new TaskListShared(sharing.Id, sharing.UserId, at, token, ownerName, code)];

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
                if (joining.UserId == join.UserId || joining.HasMember(join.UserId))
                {
                    return [];
                }

                return joining.CheckInvite(join.Token, join.Code, at) switch
                {
                    InviteCheck.Open when joining._members.Count >= ContentLimits.MembersPerList =>
                        throw new DomainRejectedException("This list already has as many members as it can hold."),
                    InviteCheck.Open =>
                        [new TaskListJoined(joining.Id, joining.UserId, at, join.UserId, RequireMemberName(join.DisplayName))],
                    InviteCheck.WrongCode =>
                        [new InviteCodeRejected(joining.Id, joining.UserId, at, joining.WrongCodes + 1)],
                    InviteCheck.Expired => throw new DomainRejectedException("This invite has expired."),
                    _ => throw new DomainRejectedException("This invite link no longer works.")
                };

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
                InviteCode = shared.Code;
                InviteExpiresAt = shared.At + InviteLife;
                WrongCodes = 0;
                OwnerName = shared.OwnerName;
                break;
            case TaskListSharingStopped:
                CloseInvite();
                break;
            case InviteCodeRejected rejected:
                WrongCodes = rejected.WrongCodes;
                if (WrongCodes >= MostWrongCodes)
                {
                    CloseInvite();
                }

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

    void CloseInvite()
    {
        InviteToken = null;
        InviteCode = null;
        InviteExpiresAt = null;
    }

    static TaskList Live(TaskList? list)
    {
        if (list is null || list.Deleted)
        {
            throw new DomainRejectedException("That list no longer exists.", Unrecoverable: true);
        }

        return list;
    }

    internal static TaskList Require(TaskList? list, Guid userId)
    {
        var existing = Live(list);
        if (existing.UserId != userId)
        {
            throw new DomainRejectedException("That list belongs to somebody else.", Unrecoverable: true);
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
            : ContentLimits.Within(name.Trim(), ContentLimits.Name, "A list name");

    static string RequireMemberName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A member needs a name.")
            : ContentLimits.Cut(name.Trim(), ContentLimits.Name);
}
