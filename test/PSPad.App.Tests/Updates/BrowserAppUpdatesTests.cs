using Microsoft.JSInterop;
using PSPad.App.Updates;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Updates;

[UnitTest]
public class BrowserAppUpdatesTests
{
    [Fact]
    public async Task ItSubscribesToTheUpdatesModule()
    {
        var js = new RecordingJsRuntime();

        await using var updates = new BrowserAppUpdates(js);
        await updates.Subscribed;

        Assert.Equal("./js/updates.js", js.Imported);
        Assert.Contains("subscribe", js.Module.Calls);
    }

    [Fact]
    public async Task AWaitingVersionIsAnnouncedOnce()
    {
        await using var updates = new BrowserAppUpdates(new RecordingJsRuntime());
        var announcements = 0;
        updates.Available += () => announcements++;

        updates.OnUpdateAvailable();
        updates.OnUpdateAvailable();

        Assert.True(updates.IsAvailable);
        Assert.Equal(1, announcements);
    }

    [Fact]
    public async Task ApplyingAsksTheModuleToActivateTheWaitingVersion()
    {
        var js = new RecordingJsRuntime();
        await using var updates = new BrowserAppUpdates(js);

        await updates.ApplyAsync();

        Assert.Equal(["subscribe", "activate"], js.Module.Calls);
    }

    sealed class RecordingJsRuntime : IJSRuntime
    {
        public RecordingModule Module { get; } = new();

        public string? Imported { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Imported = args?[0] as string;
            return new((TValue)(object)Module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    sealed class RecordingModule : IJSObjectReference
    {
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);
            return new(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
