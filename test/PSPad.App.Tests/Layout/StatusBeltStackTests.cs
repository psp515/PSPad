using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class StatusBeltStackTests : Bunit.TestContext
{
    readonly StatusBelts _belts = new();

    public StatusBeltStackTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_belts);
    }

    [Fact]
    public void NothingIsDrawnWhileThereAreNoBelts()
    {
        var stack = Render<StatusBeltStack>();

        Assert.Empty(stack.FindAll(".pspad-belts"));
    }

    [Fact]
    public void ABeltShowsItsTextAsAStatus()
    {
        _belts.Show(BeltKind.Update, "A new version of PSPad is ready.");

        var stack = Render<StatusBeltStack>();

        var belt = stack.Find(".pspad-belt");
        Assert.Equal("status", belt.GetAttribute("role"));
        Assert.Contains("A new version of PSPad is ready.", belt.TextContent);
    }

    [Theory]
    [InlineData(BeltKind.Update, "pspad-belt-info")]
    [InlineData(BeltKind.Offline, "pspad-belt-warning")]
    [InlineData(BeltKind.Rejected, "pspad-belt-error")]
    public void EachKindCarriesItsSeverity(BeltKind kind, string tone)
    {
        _belts.Show(kind, "text");

        var stack = Render<StatusBeltStack>();

        Assert.Contains(tone, stack.Find(".pspad-belt").ClassList);
        Assert.NotEmpty(stack.FindAll(".pspad-belt .mud-alert-icon"));
    }

    [Fact]
    public void ABeltShownLaterAppearsWithoutARerender()
    {
        var stack = Render<StatusBeltStack>();

        stack.InvokeAsync(() => _belts.Show(BeltKind.Offline, "offline"));

        stack.WaitForAssertion(() => Assert.Contains("offline", stack.Find(".pspad-belt").TextContent));
    }

    [Fact]
    public void TheNewestBeltIsOnTop()
    {
        _belts.Show(BeltKind.Update, "update");
        _belts.Show(BeltKind.Rejected, "rejected");

        var stack = Render<StatusBeltStack>();

        var texts = stack.FindAll(".pspad-belt-text").Select(text => text.TextContent.Trim());
        Assert.Equal(["rejected", "update"], texts);
    }

    [Fact]
    public void TheActionInvokesItsCallback()
    {
        var reloads = 0;
        _belts.Show(BeltKind.Update, "update", "Reload", () =>
        {
            reloads++;
            return Task.CompletedTask;
        });
        var stack = Render<StatusBeltStack>();

        var action = stack.Find(".pspad-belt-action");
        Assert.Equal("Reload", action.TextContent.Trim());
        action.Click();

        Assert.Equal(1, reloads);
    }

    [Fact]
    public void ABeltWithoutAnActionHasNoButtonForIt()
    {
        _belts.Show(BeltKind.Offline, "offline");

        var stack = Render<StatusBeltStack>();

        Assert.Empty(stack.FindAll(".pspad-belt-action"));
    }

    [Fact]
    public void TheCrossDismissesTheBelt()
    {
        _belts.Show(BeltKind.Offline, "offline");
        var stack = Render<StatusBeltStack>();

        stack.Find(".pspad-belt [aria-label='Dismiss']").Click();

        Assert.Empty(_belts.Visible);
        stack.WaitForAssertion(() => Assert.Empty(stack.FindAll(".pspad-belt")));
    }
}
