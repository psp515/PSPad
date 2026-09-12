using PSPad.Contracts;
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
    public void AnUnknownNameResolvesToNothing()
    {
        Assert.Null(CommandCatalogue.Resolve("DropDatabase"));
    }
}
