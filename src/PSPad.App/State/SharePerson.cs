namespace PSPad.App.State;

public sealed record SharePerson(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);
