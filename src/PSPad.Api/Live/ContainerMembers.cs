namespace PSPad.Api.Live;

public sealed record ContainerMembers(Guid OwnerId, IReadOnlyList<Guid> MemberIds);
