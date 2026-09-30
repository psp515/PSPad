using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.App.State.Viewport;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class MasonryGridTests : Bunit.TestContext
{
    [Theory]
    [InlineData(Breakpoint.Xs, 1)]
    [InlineData(Breakpoint.Sm, 2)]
    [InlineData(Breakpoint.Md, 2)]
    [InlineData(Breakpoint.Lg, 3)]
    [InlineData(Breakpoint.Xl, 4)]
    [InlineData(Breakpoint.Xxl, 4)]
    public void ColumnsFollowTheBreakpoints(Breakpoint breakpoint, int expected)
    {
        Assert.Equal(expected, MasonryGrid<string>.ColumnsFor(breakpoint));
    }

    [Fact]
    public void ItemsAreDealtAcrossColumnsInOrder()
    {
        var columns = MasonryGrid<string>.Columns(["a", "b", "c", "d", "e"], 2);

        Assert.Equal(["a", "c", "e"], columns[0]);
        Assert.Equal(["b", "d"], columns[1]);
    }

    [Fact]
    public void FewerItemsThanColumnsLeaveTheRestEmpty()
    {
        var columns = MasonryGrid<string>.Columns(["a"], 3);

        Assert.Equal(3, columns.Count);
        Assert.Equal(["a"], columns[0]);
        Assert.Empty(columns[1]);
        Assert.Empty(columns[2]);
    }

    [Fact]
    public void ItRendersOneStackPerColumnForTheCurrentBreakpoint()
    {
        Arrange(Breakpoint.Lg);

        var grid = RenderGrid(["a", "b", "c", "d", "e"]);

        var columns = grid.FindAll(".pspad-masonry-column");
        Assert.Equal(3, columns.Count);
        Assert.Equal(["a", "d"], columns[0].QuerySelectorAll(".item").Select(item => item.TextContent));
    }

    [Fact]
    public void ABreakpointChangeReflowsTheColumns()
    {
        var breakpoints = Arrange(Breakpoint.Lg);
        var grid = RenderGrid(["a", "b", "c"]);

        breakpoints.ChangeTo(Breakpoint.Xs);

        grid.WaitForAssertion(() => Assert.Single(grid.FindAll(".pspad-masonry-column")));
    }

    AppTestHost.FakeBreakpoints Arrange(Breakpoint breakpoint)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var breakpoints = new AppTestHost.FakeBreakpoints(breakpoint);
        Services.AddSingleton<IBreakpoints>(breakpoints);
        return breakpoints;
    }

    IRenderedComponent<MasonryGrid<string>> RenderGrid(IReadOnlyList<string> items) =>
        Render<MasonryGrid<string>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.Key, item => item)
            .Add(p => p.ItemTemplate, item => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "item");
                builder.AddContent(2, item);
                builder.CloseElement();
            }));
}
