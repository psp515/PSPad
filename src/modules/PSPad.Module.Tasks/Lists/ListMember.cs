namespace PSPad.Module.Tasks.Lists;

public sealed record ListMember(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);
