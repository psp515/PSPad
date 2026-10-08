using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class TimeRowTests : Bunit.TestContext
{
    public TimeRowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void EndWaitsForAStart()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        Assert.True(row.Find(".pspad-time-end input").HasAttribute("disabled"));
    }

    [Fact]
    public async Task PickingAStartSendsATimeWithoutAnEnd()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, (TaskTime?)null)
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        await row.InvokeAsync(() => row.Instance.StartChangedAsync(new TimeSpan(9, 30, 0)));

        Assert.Equal(new TaskTime(new TimeOnly(9, 30), null), sent);
    }

    [Fact]
    public async Task AnEndBeforeTheStartShowsAnErrorAndSendsNothing()
    {
        var calls = 0;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? _) => calls++));

        await row.InvokeAsync(() => row.Instance.EndChangedAsync(new TimeSpan(13, 0, 0)));

        Assert.Equal(0, calls);
        Assert.Contains("End must be after start", row.Find(".pspad-time-error").TextContent);
    }

    [Fact]
    public void ClearingRemovesBoth()
    {
        TaskTime? sent = new(new TimeOnly(9, 0), null);
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0)))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-clear").Click();

        Assert.Null(sent);
    }

    [Theory]
    [InlineData(9, 30, 11, 0, "09:30–11:00")]
    [InlineData(9, 30, -1, 0, "09:30")]
    public void ItDescribesATime(int sh, int sm, int eh, int em, string expected) =>
        Assert.Equal(expected, TimeRow.Describe(new TaskTime(new TimeOnly(sh, sm), eh < 0 ? null : new TimeOnly(eh, em))));
}
