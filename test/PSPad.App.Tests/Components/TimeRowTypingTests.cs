using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class TimeRowTypingTests : Bunit.TestContext
{
    readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public TimeRowTypingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("pl-PL")]
    public void TypingAStartIntoThePickerSendsTheTime(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        TaskTime? sent = null;
        var host = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TimeRow>(1);
            builder.AddAttribute(2, nameof(TimeRow.Value), (TaskTime?)null);
            builder.AddAttribute(3, nameof(TimeRow.ValueChanged),
                EventCallback.Factory.Create<TaskTime?>(this, (TaskTime? time) => sent = time));
            builder.CloseComponent();
        });

        host.Find(".pspad-time-start input").Change("09:30");

        host.WaitForAssertion(
            () => Assert.Equal(new TaskTime(new TimeOnly(9, 30), null), sent),
            TimeSpan.FromSeconds(10));
        Assert.Equal("09:30", host.Find(".pspad-time-start input").GetAttribute("value"));
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        base.Dispose(disposing);
    }
}
