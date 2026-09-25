using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class AreaQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNoArea()
    {
        Assert.Null(AreaQuery.From("https://pspad.local/areas/" + Guid.NewGuid()));
        Assert.False(AreaQuery.IsNew("https://pspad.local/"));
    }

    [Fact]
    public void AUrlWithAnAreaOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, AreaQuery.From($"https://pspad.local/areas/{id}?area={id}"));
    }

    [Fact]
    public void ANewAreaUrlIsNewAndOpensNoExistingArea()
    {
        var uri = AreaQuery.ForNew("https://pspad.local/today?task=" + Guid.NewGuid());

        Assert.Equal("https://pspad.local/today?area=new", uri);
        Assert.True(AreaQuery.IsNew(uri));
        Assert.Null(AreaQuery.From(uri));
    }

    [Fact]
    public void EditingAnAreaKeepsTheScreen()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"https://pspad.local/areas/{id}?area={id}",
            AreaQuery.For($"https://pspad.local/areas/{id}", id));
    }
}
