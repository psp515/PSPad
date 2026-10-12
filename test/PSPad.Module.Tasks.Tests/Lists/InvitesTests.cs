using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class InvitesTests
{
    const string Token = "abcdefghijklmnopqrstuvwxyz";
    static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset Until = Now.AddMinutes(10);

    [Fact]
    public void RightTokenAndCodeBeforeExpiryIsOpen() =>
        Assert.Equal(InviteCheck.Open, Invites.Check(Token, "K7M4PX", Until, Token, "k7m-4px", Now));

    [Fact]
    public void WrongCodeOnALiveInviteIsWrongCode() =>
        Assert.Equal(InviteCheck.WrongCode, Invites.Check(Token, "K7M4PX", Until, Token, "AAAAAA", Now));

    [Fact]
    public void RightCodeAfterExpiryIsExpired() =>
        Assert.Equal(InviteCheck.Expired, Invites.Check(Token, "K7M4PX", Until, Token, "K7M4PX", Until));

    [Fact]
    public void WrongCodeAfterExpiryIsUnknown() =>
        Assert.Equal(InviteCheck.Unknown, Invites.Check(Token, "K7M4PX", Until, Token, "AAAAAA", Until));

    [Fact]
    public void WrongOrClearedTokenIsUnknown()
    {
        Assert.Equal(InviteCheck.Unknown, Invites.Check(Token, "K7M4PX", Until, Token + "x", "K7M4PX", Now));
        Assert.Equal(InviteCheck.Unknown, Invites.Check(null, null, null, Token, "K7M4PX", Now));
    }

    [Fact]
    public void ShortTokenIsRejected() =>
        Assert.Throws<DomainRejectedException>(() => Invites.RequireToken("short"));

    [Fact]
    public void MalformedCodeIsRejected() =>
        Assert.Throws<DomainRejectedException>(() => Invites.RequireCode("K7M4P"));

    [Fact]
    public void CodeIsNormalized() => Assert.Equal("K7M4PX", Invites.RequireCode("k7m-4px"));
}
