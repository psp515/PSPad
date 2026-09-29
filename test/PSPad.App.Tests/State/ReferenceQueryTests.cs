using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ReferenceQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNothing()
    {
        Assert.Null(ReferenceQuery.From("https://pspad.local/lists/" + Guid.NewGuid()));
    }

    [Fact]
    public void AUrlWithAnItemOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, ReferenceQuery.From($"https://pspad.local/?item={id}"));
    }

    [Fact]
    public void ANonGuidItemOpensNothing()
    {
        Assert.Null(ReferenceQuery.From("https://pspad.local/?item=nonsense"));
    }

    [Fact]
    public void ANewItemQueryNamesTheListItGoesInto()
    {
        var list = Guid.NewGuid();

        Assert.Equal(list, ReferenceQuery.NewItemListFrom($"https://pspad.local/lists/{list}?item=new&list={list}"));
    }

    [Fact]
    public void ANewItemQueryOpensNoExistingItem()
    {
        Assert.Null(ReferenceQuery.From($"https://pspad.local/?item=new&list={Guid.NewGuid()}"));
    }

    [Fact]
    public void AnExistingItemQueryIsNotANewItem()
    {
        Assert.Null(ReferenceQuery.NewItemListFrom($"https://pspad.local/?item={Guid.NewGuid()}"));
    }

    [Fact]
    public void ForItemReplacesAnyOpenQueryOnTheSameScreen()
    {
        var list = Guid.NewGuid();
        var item = Guid.NewGuid();

        Assert.Equal(
            $"https://pspad.local/lists/{list}?item={item}",
            ReferenceQuery.ForItem($"https://pspad.local/lists/{list}?item={Guid.NewGuid()}", item));
    }

    [Fact]
    public void ForNewItemReplacesAnyOpenQueryOnTheSameScreen()
    {
        var list = Guid.NewGuid();

        Assert.Equal(
            $"https://pspad.local/lists/{list}?item=new&list={list}",
            ReferenceQuery.ForNewItem($"https://pspad.local/lists/{list}?item={Guid.NewGuid()}", list));
    }
}
