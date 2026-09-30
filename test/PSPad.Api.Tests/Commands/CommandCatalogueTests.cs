using PSPad.Contracts;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Commands;

[UnitTest]
public class CommandCatalogueTests
{
    [Fact]
    public void ACommandResolvesByItsTypeName()
    {
        Assert.Equal(typeof(CreateArea), CommandCatalogue.Resolve("CreateArea"));
    }

    [Fact]
    public void APresentationCommandResolves()
    {
        Assert.Equal(typeof(ReorderLists), CommandCatalogue.Resolve("ReorderLists"));
    }

    [Fact]
    public void AnUnknownNameResolvesToNothing()
    {
        Assert.Null(CommandCatalogue.Resolve("DropDatabase"));
    }
}
