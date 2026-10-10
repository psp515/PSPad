using Microsoft.JSInterop;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class NavGroupStateTests
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
    public async Task EveryGroupStartsExpanded()
    {
        var state = new NavGroupState(new FakeJsRuntime());
        await state.LoadAsync();

        Assert.True(state.IsExpanded(NavGroupState.Budgets));
        Assert.True(state.IsExpanded(NavGroupState.Areas));
    }

    [Fact]
    public async Task CollapsingIsStoredAndSurvivesAReload()
    {
        var js = new FakeJsRuntime();
        var state = new NavGroupState(js);
        await state.SetAsync(NavGroupState.Areas, false);

        Assert.Equal("areas", js.Stored);

        var reloaded = new NavGroupState(js);
        await reloaded.LoadAsync();
        Assert.False(reloaded.IsExpanded(NavGroupState.Areas));
        Assert.True(reloaded.IsExpanded(NavGroupState.Budgets));
    }
}
