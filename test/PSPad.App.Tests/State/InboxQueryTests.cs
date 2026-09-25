using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class InboxQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNoItem()
    {
        Assert.Null(InboxQuery.From("https://pspad.local/inbox"));
        Assert.False(InboxQuery.IsNew("https://pspad.local/inbox"));
    }

    [Fact]
    public void AUrlWithAnItemOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, InboxQuery.From($"https://pspad.local/inbox?inbox={id}"));
    }

    [Fact]
    public void ANewItemUrlIsNewAndOpensNoExistingItem()
    {
        var uri = InboxQuery.ForNew("https://pspad.local/inbox?task=" + Guid.NewGuid());

        Assert.Equal("https://pspad.local/inbox?inbox=new", uri);
        Assert.True(InboxQuery.IsNew(uri));
        Assert.Null(InboxQuery.From(uri));
    }

    [Fact]
    public void OpeningAnItemKeepsTheScreen()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"https://pspad.local/inbox?inbox={id}", InboxQuery.For("https://pspad.local/inbox", id));
    }
}
