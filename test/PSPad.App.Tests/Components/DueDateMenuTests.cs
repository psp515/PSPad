using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class DueDateMenuTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 9, 24);

    readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    readonly List<DateOnly?> _sent = [];

    public DueDateMenuTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    [Fact]
    public void ChoosingTomorrowSendsTomorrow()
    {
        var host = Host(value: null, clearable: true);

        host.Find("button").Click();
        Item(host, "Tomorrow").Click();

        Assert.Equal([Today.AddDays(1)], _sent);
    }

    [Fact]
    public void ChoosingTheCurrentDaySendsNothing()
    {
        var host = Host(value: Today, clearable: true);

        host.Find("button").Click();
        Item(host, "Today").Click();

        Assert.Empty(_sent);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ClearShowsOnlyWhenClearableAndSet(bool clearable, bool set)
    {
        var host = Host(value: set ? Today : null, clearable: clearable);

        host.Find("button").Click();

        Assert.DoesNotContain(host.FindAll("[role=menuitem]"), item => item.TextContent.Contains("Clear date"));
    }

    [Fact]
    public void ClearSendsNoDate()
    {
        var host = Host(value: Today, clearable: true);

        host.Find("button").Click();
        Item(host, "Clear date").Click();

        Assert.Equal([(DateOnly?)null], _sent);
    }

    [Fact]
    public void ASetDateReadsRelatively()
    {
        var host = Host(value: Today.AddDays(-1), clearable: true);

        Assert.Contains("Yesterday", host.Find("button").TextContent);
    }

    [Fact]
    public void AnEmptyDateShowsALabelledIcon()
    {
        var host = Host(value: null, clearable: true);

        Assert.NotNull(host.Find("button[aria-label='Set a deadline']"));
    }

    static AngleSharp.Dom.IElement Item(IRenderedComponent<IComponent> host, string text) =>
        host.FindAll("[role=menuitem]").Single(item => item.TextContent.Contains(text));

    IRenderedComponent<IComponent> Host(DateOnly? value, bool clearable) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<DueDateMenu>(1);
            builder.AddAttribute(2, nameof(DueDateMenu.Value), value);
            builder.AddAttribute(3, nameof(DueDateMenu.Today), Today);
            builder.AddAttribute(4, nameof(DueDateMenu.Clearable), clearable);
            builder.AddAttribute(6, nameof(DueDateMenu.EmptyLabel), "Set a deadline");
            builder.AddAttribute(7, nameof(DueDateMenu.ValueChanged),
                EventCallback.Factory.Create<DateOnly?>(this, (DateOnly? day) => _sent.Add(day)));
            builder.CloseComponent();
        });

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        base.Dispose(disposing);
    }
}
