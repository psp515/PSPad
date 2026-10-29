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
}
