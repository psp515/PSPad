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
    public async Task AnEndEqualToTheStartShowsAnErrorAndSendsNothing()
    {
        var calls = 0;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? _) => calls++));

        await row.InvokeAsync(() => row.Instance.EndChangedAsync(new TimeSpan(14, 0, 0)));

        Assert.Equal(0, calls);
        Assert.Contains("End must differ from start", row.Find(".pspad-time-error").TextContent);
    }

    [Fact]
    public async Task AnEndBeforeTheStartIsSentAsOvernight()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(22, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        await row.InvokeAsync(() => row.Instance.EndChangedAsync(new TimeSpan(1, 0, 0)));

        Assert.Equal(new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0)), sent);
        Assert.Empty(row.FindAll(".pspad-time-error"));
    }

    [Fact]
    public void AnOvernightTimeHintsItEndsTheNextDay()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0))));

        Assert.Equal("Ends the next day (+1)", row.Find(".pspad-time-hint").TextContent.Trim());
    }

    [Fact]
    public void ATimeWithinTheDayHasNoHint()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.Empty(row.FindAll(".pspad-time-hint"));
    }

    [Fact]
    public async Task AStaleErrorIsForgottenWhenTheValueChanges()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null)));
        await row.InvokeAsync(() => row.Instance.EndChangedAsync(new TimeSpan(14, 0, 0)));
        Assert.NotEmpty(row.FindAll(".pspad-time-error"));

        row.Render(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), new TimeOnly(15, 0))));

        Assert.Empty(row.FindAll(".pspad-time-error"));
    }

    [Fact]
    public void TheClearButtonLinesUpWithTheOtherProperties()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), null)));

        Assert.Contains("pspad-property-clear", row.Find(".pspad-time-clear").ClassName);
    }

    [Fact]
    public void WithoutATimeThereIsNoClearButtonButItsColumnIsKept()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        Assert.Empty(row.FindAll(".pspad-time-clear"));
        Assert.Single(row.FindAll(".pspad-time-clear-placeholder"));
    }

    [Fact]
    public void TheFieldsAreNamed()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.Equal("Start", row.Find(".pspad-time-start input").GetAttribute("aria-label"));
        Assert.Equal("End", row.Find(".pspad-time-end input").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(".pspad-time-start")]
    [InlineData(".pspad-time-end")]
    public void EachPickerEndsInAClockIcon(string picker)
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        var adornment = row.Find($"{picker} .mud-input-adornment-end");
        Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.Schedule), adornment.InnerHtml);
    }

    [Fact]
    public void ThePickersStayTypeable()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.False(row.Find(".pspad-time-start input").HasAttribute("readonly"));
        Assert.False(row.Find(".pspad-time-end input").HasAttribute("readonly"));
    }

    [Fact]
    public void TheClockIconOpensThePicker()
    {
        var host = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TimeRow>(1);
            builder.AddAttribute(2, nameof(TimeRow.Value), (TaskTime?)new TaskTime(new TimeOnly(9, 0), null));
            builder.CloseComponent();
        });
        Assert.Empty(host.FindAll(".mud-picker-content"));

        host.Find(".pspad-time-start .mud-input-adornment-end button").Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".mud-picker-content")));
    }

    [Fact]
    public void FromAndToSitOnTwoRowsBesideTheTimeLabel()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.Equal("Time", row.Find(".pspad-time-head .pspad-property-label").TextContent);
        Assert.NotNull(row.Find(".pspad-time-head .pspad-time-icon"));
        var lines = row.FindAll(".pspad-time-lines > .pspad-time-line");
        Assert.Equal(2, lines.Count);
        Assert.Equal("From", lines[0].QuerySelector(".pspad-time-line-label")!.TextContent);
        Assert.NotNull(lines[0].QuerySelector(".pspad-time-start"));
        Assert.Equal("To", lines[1].QuerySelector(".pspad-time-line-label")!.TextContent);
        Assert.NotNull(lines[1].QuerySelector(".pspad-time-end"));
    }

    [Fact]
    public void TheClearButtonSitsOnTheFromRow()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), null)));

        var lines = row.FindAll(".pspad-time-line");
        Assert.NotNull(lines[0].QuerySelector(".pspad-time-clear"));
        Assert.Null(lines[1].QuerySelector(".pspad-time-clear"));
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
    [InlineData(22, 0, 1, 0, "22:00–01:00 (+1)")]
    public void ItDescribesATime(int sh, int sm, int eh, int em, string expected) =>
        Assert.Equal(expected, TimeRow.Describe(new TaskTime(new TimeOnly(sh, sm), eh < 0 ? null : new TimeOnly(eh, em))));
}
