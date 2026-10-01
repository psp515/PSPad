using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class InviteTokenTests
{
    [Fact]
    public void ATokenIs24UrlSafeCharacters()
    {
        var token = InviteToken.New();

        Assert.Equal(24, token.Length);
        Assert.DoesNotContain(token, c => c is '+' or '/' or '=');
    }

    [Fact]
    public void TheLinkPointsAtJoin() =>
        Assert.Equal("https://pspad.example/join/abc", InviteToken.LinkFor("https://pspad.example/", "abc"));
}
