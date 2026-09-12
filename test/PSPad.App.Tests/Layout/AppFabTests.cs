using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AppFabTests : Bunit.TestContext
{
    [Fact]
    public void WithNoActionItRendersNothing()
    {
        Arrange();

        var fab = Render<AppFab>();

        Assert.Empty(fab.Markup.Trim());
    }

    [Fact]
    public async Task TappingItInvokesThePrimaryAction()
    {
        var context = Arrange();
        var invoked = false;
        context.Set(new FabAction("Capture", Icons.Material.Filled.Add, () =>
        {
            invoked = true;
            return Task.CompletedTask;
        }));

        var fab = Render<AppFab>();
        await fab.FindAll("button")[0].ClickAsync(new());

        Assert.True(invoked);
    }

    [Fact]
    public void WithSecondaryActionsItShowsACaret()
    {
        var context = Arrange();
        context.Set(
            new FabAction("Capture", Icons.Material.Filled.Add, () => Task.CompletedTask),
            new FabAction("New area", Icons.Material.Filled.Folder, () => Task.CompletedTask));

        var fab = Render<AppFab>();

        Assert.Equal(2, fab.FindAll("button").Count);
    }

    [Fact]
    public void WithoutSecondaryActionsThereIsNoCaret()
    {
        var context = Arrange();
        context.Set(new FabAction("Capture", Icons.Material.Filled.Add, () => Task.CompletedTask));

        var fab = Render<AppFab>();

        Assert.Single(fab.FindAll("button"));
    }

    FabContext Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        var context = new FabContext();
        Services.AddSingleton(context);
        return context;
    }
}
