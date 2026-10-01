using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record ListAccess(Guid OwnerId, Guid ActorId)
{
    public bool ByOwner => OwnerId == ActorId;

    public static ListAccess Owner(Guid userId) => new(userId, userId);

    public static ListAccess To(TaskList? list, Guid actorId)
    {
        if (list is null || list.Deleted)
        {
            throw new DomainRejectedException("That list no longer exists.");
        }

        return list.UserId == actorId || list.HasMember(actorId)
            ? new ListAccess(list.UserId, actorId)
            : throw new DomainRejectedException("That list belongs to somebody else.");
    }
}
