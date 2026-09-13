using Microsoft.JSInterop;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class CardCollapseStateTests
{
    sealed class FakeJsRuntime : IJSRuntime
    {
        public string? Stored { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "localStorage.setItem")
            {
                Stored = args?[1] as string;
            }

            return ValueTask.FromResult((TValue)(object)(Stored ?? "")!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    [Fact]
    public async Task EverythingStartsExpanded()
    {
        var state = new CardCollapseState(new FakeJsRuntime());
        await state.LoadAsync();

        Assert.False(state.IsCollapsed(Guid.NewGuid()));
    }

    [Fact]
    public async Task TogglingCollapsesThatListAndNoOther()
    {
        var state = new CardCollapseState(new FakeJsRuntime());
        await state.LoadAsync();
        var collapsed = Guid.NewGuid();
        var untouched = Guid.NewGuid();

        await state.ToggleAsync(collapsed);

        Assert.True(state.IsCollapsed(collapsed));
        Assert.False(state.IsCollapsed(untouched));
    }

    [Fact]
    public async Task TogglingTwiceExpandsAgain()
    {
        var state = new CardCollapseState(new FakeJsRuntime());
        await state.LoadAsync();
        var list = Guid.NewGuid();

        await state.ToggleAsync(list);
        await state.ToggleAsync(list);

        Assert.False(state.IsCollapsed(list));
    }

    [Fact]
    public async Task CollapsedListsSurviveAReload()
    {
        var js = new FakeJsRuntime();
        var list = Guid.NewGuid();
        var first = new CardCollapseState(js);
        await first.LoadAsync();
        await first.ToggleAsync(list);

        var second = new CardCollapseState(js);
        await second.LoadAsync();

        Assert.True(second.IsCollapsed(list));
    }

    [Fact]
    public async Task TogglingRaisesChanged()
    {
        var state = new CardCollapseState(new FakeJsRuntime());
        await state.LoadAsync();
        var raised = 0;
        state.Changed += () => raised++;

        await state.ToggleAsync(Guid.NewGuid());

        Assert.Equal(1, raised);
    }
}
