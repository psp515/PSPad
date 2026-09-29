using Microsoft.JSInterop;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class LastAreaTests
{
    sealed class FakeJsRuntime : IJSRuntime
    {
        public string? Stored { get; set; }

        public bool Throws { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (Throws)
            {
                throw new JSException("storage blocked");
            }

            if (identifier == "localStorage.setItem")
            {
                Stored = args?[1] as string;
                return ValueTask.FromResult(default(TValue)!);
            }

            return ValueTask.FromResult((TValue)(object?)Stored!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    [Fact]
    public async Task NothingIsRememberedAtFirst()
    {
        Assert.Null(await new LastArea(new FakeJsRuntime()).ReadAsync());
    }

    [Fact]
    public async Task ItRemembersTheLastArea()
    {
        var js = new FakeJsRuntime();
        var areaId = Guid.NewGuid();

        await new LastArea(js).RememberAsync(areaId);

        Assert.Equal(areaId, await new LastArea(js).ReadAsync());
    }

    [Fact]
    public async Task GarbageInStorageReadsAsNothing()
    {
        var js = new FakeJsRuntime { Stored = "not-a-guid" };

        Assert.Null(await new LastArea(js).ReadAsync());
    }

    [Fact]
    public async Task BlockedStorageNeitherThrowsNorRemembers()
    {
        var js = new FakeJsRuntime { Throws = true };
        var lastArea = new LastArea(js);

        await lastArea.RememberAsync(Guid.NewGuid());

        Assert.Null(await lastArea.ReadAsync());
    }
}
