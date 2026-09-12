using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class HistoryPageTests : Bunit.TestContext
{
    [Fact]
    public void EntriesRenderNewestFirstWithTheirDescriptions()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IHistorySource>(new FakeHistory(
            new HistoryEntry(9, DateTimeOffset.UnixEpoch, "TodoTask", Guid.NewGuid(), "Completed a task"),
            new HistoryEntry(8, DateTimeOffset.UnixEpoch, "Area", Guid.NewGuid(), "Created an area")));

        var page = Render<HistoryPage>();

        var completed = page.Markup.IndexOf("Completed a task", StringComparison.Ordinal);
        var created = page.Markup.IndexOf("Created an area", StringComparison.Ordinal);

        Assert.True(completed >= 0 && completed < created);
    }

    sealed class FakeHistory(params HistoryEntry[] entries) : IHistorySource
    {
        public Task<IReadOnlyList<HistoryEntry>> ReadAsync(long? before, int limit) =>
            Task.FromResult<IReadOnlyList<HistoryEntry>>(entries);
    }
}
