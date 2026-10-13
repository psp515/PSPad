using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed record InviteState(
    string? Token, string? Code, DateTimeOffset? ExpiresAt, bool ClosedByWrongCodes, int MemberCount)
{
    public bool NeverShared => Token is null && MemberCount == 0;

    public bool IsLiveAt(DateTimeOffset now) => Invites.IsLiveAt(Token, Code, ExpiresAt, now);
}
