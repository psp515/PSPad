using Bunit;
using Bunit.Rendering;
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
        Services.AddLocalization();
    }

    [Fact]
    public void WithoutAStartOnlyFromIsShown()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        Assert.Single(row.FindAll(".pspad-time-lines > .pspad-time-line"));
        Assert.NotNull(row.Find(".pspad-time-start"));
        Assert.Empty(row.FindAll(".pspad-time-end"));
    }

    [Fact]
    public void SettingAStartBringsToInUnderFrom()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        row.Render(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(12, 0), null)));

        var lines = row.FindAll(".pspad-time-lines > .pspad-time-line");
        Assert.Equal(2, lines.Count);
        Assert.NotNull(lines[1].QuerySelector(".pspad-time-end"));
        Assert.False(row.Find(".pspad-time-end input").HasAttribute("disabled"));
    }

    [Fact]
    public void TypingAStartSendsATimeWithoutAnEnd()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, (TaskTime?)null)
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-start input").Change("09:30");

        Assert.Equal(new TaskTime(new TimeOnly(9, 30), null), sent);
    }

    [Fact]
    public void AnEndEqualToTheStartShowsAnErrorAndSendsNothing()
    {
        var calls = 0;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? _) => calls++));

        row.Find(".pspad-time-end input").Change("14:00");

        Assert.Equal(0, calls);
        Assert.Contains("End must differ from start", row.Find(".pspad-time-error").TextContent);
    }

    [Fact]
    public void AnEndBeforeTheStartIsSentAsOvernight()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(22, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-end input").Change("01:00");

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
    public void AStaleErrorIsForgottenWhenTheValueChanges()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null)));
        row.Find(".pspad-time-end input").Change("14:00");
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
    public void TheClockIconOpensAClockDialog()
    {
        var host = RenderWithOverlays(new TaskTime(new TimeOnly(9, 0), null));
        Assert.Empty(host.FindAll(".pspad-time-dialog"));

        host.Find(".pspad-time-start .mud-input-adornment-end button").Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".pspad-time-dialog .mud-picker-static")));
    }

    [Fact]
    public async Task ATimePickedFromTheEndClockIsSentAndClosesTheDialog()
    {
        TaskTime? sent = null;
        var host = RenderWithOverlays(new TaskTime(new TimeOnly(9, 0), null), time => sent = time);
        host.Find(".pspad-time-end .mud-input-adornment-end button").Click();
        host.WaitForElement(".pspad-time-dialog");

        var clock = host.FindComponent<MudBlazor.MudTimePicker>();
        await host.InvokeAsync(() => clock.Instance.TimeChanged.InvokeAsync(new TimeSpan(10, 30, 0)));

        Assert.Equal(new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 30)), sent);
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".pspad-time-dialog")));
    }

    [Theory]
    [InlineData("14", 14, 0)]
    [InlineData("9", 9, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("1430", 14, 30)]
    [InlineData("14.30", 14, 30)]
    [InlineData(" 7:05 ", 7, 5)]
    public void TypedTextIsReadLeniently(string typed, int hour, int minute)
    {
        Assert.True(TimeText.TryParse(typed, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    [InlineData("12:3")]
    public void UnreadableTextIsRejected(string typed) => Assert.False(TimeText.TryParse(typed, out _));

    [Fact]
    public void EmptyTextReadsAsNoTime()
    {
        Assert.True(TimeText.TryParse("  ", out var time));
        Assert.Null(time);
    }

    [Fact]
    public void TypingJustAnHourSetsTheFullHour()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, (TaskTime?)null)
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-start input").Change("14");

        Assert.Equal(new TaskTime(new TimeOnly(14, 0), null), sent);
    }

    [Fact]
    public void UnreadableStartShowsAHintKeepsTheTextAndSendsNothing()
    {
        var calls = 0;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? _) => calls++));

        row.Find(".pspad-time-start input").Change("nine");

        Assert.Equal(0, calls);
        Assert.Equal("Enter a time like 14:30", row.Find(".pspad-time-error").TextContent.Trim());
        Assert.Equal("nine", row.Find(".pspad-time-start input").GetAttribute("value"));
        Assert.Contains("mud-input-error", row.Find(".pspad-time-start .mud-input").ClassName);
    }

    [Fact]
    public void EmptyingTheStartClearsTheTime()
    {
        TaskTime? sent = new(new TimeOnly(9, 0), null);
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0)))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-start input").Change("");

        Assert.Null(sent);
    }

    [Fact]
    public void EmptyingTheEndDropsOnlyTheEnd()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0)))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find(".pspad-time-end input").Change("");

        Assert.Equal(new TaskTime(new TimeOnly(9, 0), null), sent);
    }

    [Fact]
    public void TheFieldsShowTheTimeAsHoursAndMinutes()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 5), new TimeOnly(22, 0))));

        Assert.Equal("09:05", row.Find(".pspad-time-start input").GetAttribute("value"));
        Assert.Equal("22:00", row.Find(".pspad-time-end input").GetAttribute("value"));
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(TaskTime? value, Action<TaskTime?>? onChanged = null) => Render(builder =>
    {
        builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudBlazor.MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<TimeRow>(2);
        builder.AddAttribute(3, nameof(TimeRow.Value), value);
        if (onChanged is not null)
        {
            builder.AddAttribute(4, nameof(TimeRow.ValueChanged),
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TaskTime?>(this, onChanged));
        }
        builder.CloseComponent();
    });

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
