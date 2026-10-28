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
        Services.AddLocalization();
    }

    [Fact]
    public void WithoutAStartOnlyTheStartFieldIsShown()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        Assert.NotNull(row.Find("input[aria-label='Start']"));
        Assert.Empty(row.FindAll("input[aria-label='End']"));
    }

    [Fact]
    public void SettingAStartBringsTheEndFieldIn()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        row.Render(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(12, 0), null)));

        Assert.False(row.Find("input[aria-label='End']").HasAttribute("disabled"));
    }

    [Fact]
    public void TypingAStartSendsATimeWithoutAnEnd()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, (TaskTime?)null)
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find("input[aria-label='Start']").Change("09:30");

        row.WaitForAssertion(() => Assert.Equal(new TaskTime(new TimeOnly(9, 30), null), sent));
    }

    [Fact]
    public void AnEndEqualToTheStartShowsAnErrorAndSendsNothing()
    {
        var calls = 0;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? _) => calls++));

        row.Find("input[aria-label='End']").Change("14:00");

        row.WaitForAssertion(() => Assert.Contains("End must differ from start", row.Find("[role=alert]").TextContent));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void AnEndBeforeTheStartIsSentAsOvernight()
    {
        TaskTime? sent = null;
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(22, 0), null))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find("input[aria-label='End']").Change("01:00");

        row.WaitForAssertion(() => Assert.Equal(new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0)), sent));
        Assert.Empty(row.FindAll("[role=alert]"));
    }

    [Fact]
    public void AnOvernightTimeHintsItEndsTheNextDay()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0))));

        Assert.Equal("Ends the next day (+1)", row.Find("[role=status]").TextContent.Trim());
    }

    [Fact]
    public void ATimeWithinTheDayHasNoHint()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.Empty(row.FindAll("[role=status]"));
    }

    [Fact]
    public void AStaleErrorIsForgottenWhenTheValueChanges()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), null)));
        row.Find("input[aria-label='End']").Change("14:00");
        row.WaitForAssertion(() => Assert.Contains("End must differ from start", row.Find("[role=alert]").TextContent));

        row.Render(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(14, 0), new TimeOnly(15, 0))));

        Assert.Empty(row.FindAll("[role=alert]"));
    }

    [Fact]
    public void WithoutATimeThereIsNoClearButton()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, (TaskTime?)null));

        Assert.Empty(row.FindAll("button[aria-label='Clear time']"));
    }

    [Fact]
    public void ThePickersStayTypeable()
    {
        var row = Render<TimeRow>(p => p.Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0))));

        Assert.False(row.Find("input[aria-label='Start']").HasAttribute("readonly"));
        Assert.False(row.Find("input[aria-label='End']").HasAttribute("readonly"));
    }

    [Fact]
    public void TheClockButtonOpensThePicker()
    {
        var host = Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TimeRow>(1);
            builder.AddAttribute(2, nameof(TimeRow.Value), (TaskTime?)new TaskTime(new TimeOnly(9, 0), null));
            builder.CloseComponent();
        });
        var buttonsBefore = host.FindAll("button").Count;

        host.Find("button[aria-label='Pick start time']").Click();

        host.WaitForAssertion(() => Assert.True(host.FindAll("button").Count > buttonsBefore));
    }

    [Fact]
    public void ClearingRemovesBoth()
    {
        TaskTime? sent = new(new TimeOnly(9, 0), null);
        var row = Render<TimeRow>(p => p
            .Add(r => r.Value, new TaskTime(new TimeOnly(9, 0), new TimeOnly(10, 0)))
            .Add(r => r.ValueChanged, (TaskTime? time) => sent = time));

        row.Find("button[aria-label='Clear time']").Click();

        Assert.Null(sent);
    }

    [Theory]
    [InlineData(9, 30, 11, 0, "09:30–11:00")]
    [InlineData(9, 30, -1, 0, "09:30")]
    [InlineData(22, 0, 1, 0, "22:00–01:00 (+1)")]
    public void ItDescribesATime(int sh, int sm, int eh, int em, string expected) =>
        Assert.Equal(expected, TimeRow.Describe(new TaskTime(new TimeOnly(sh, sm), eh < 0 ? null : new TimeOnly(eh, em))));
}
