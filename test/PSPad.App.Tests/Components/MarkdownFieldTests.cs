using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.App.Markdown;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class MarkdownFieldTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsRenderedMarkdownByDefault()
    {
        Arrange();

        var field = Render("## Hi");

        Assert.Contains("<h2", field.Markup);
        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void ABlankEditableValueShowsTheEditorDirectly()
    {
        Arrange();

        var field = Render("");

        var textarea = field.Find(".pspad-markdown-input textarea");
        Assert.Equal("Add a description…", textarea.GetAttribute("placeholder"));
        Assert.Empty(field.FindAll("button"));
    }

    [Fact]
    public void TypingIntoTheBlankEditorAndLeavingSaves()
    {
        Arrange();
        string? saved = null;
        var field = Render("", onSave: text =>
        {
            saved = text;
            return System.Threading.Tasks.Task.FromResult(true);
        });

        field.Find("textarea").Input("Whole milk");
        field.Find("textarea").Blur();

        Assert.Equal("Whole milk", saved);
    }

    [Fact]
    public void LeavingTheBlankEditorUntouchedDoesNotSave()
    {
        Arrange();
        var saveCalled = false;
        var field = Render("", onSave: _ =>
        {
            saveCalled = true;
            return System.Threading.Tasks.Task.FromResult(true);
        });

        field.Find("textarea").Blur();

        Assert.False(saveCalled);
        Assert.NotEmpty(field.FindAll("textarea"));
    }

    [Fact]
    public void TheEditIconSwitchesToTheRawText()
    {
        Arrange();
        var field = Render("## Hi");

        field.Find(".pspad-markdown-edit").Click();

        Assert.Equal("## Hi", field.Find("textarea").TextContent);
    }

    [Fact]
    public async Task ClickingTheRenderedTextSwitchesToTheRawText()
    {
        Arrange();
        var field = Render("## Hi");

        await ClickRenderedText(field, onLink: false);

        Assert.Equal("## Hi", field.Find("textarea").TextContent);
    }

    [Fact]
    public async Task ClickingALinkInTheRenderedTextKeepsTheView()
    {
        Arrange();
        var field = Render("[site](https://example.com)");

        await ClickRenderedText(field, onLink: true);

        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void LeavingAfterAnEditSavesAndReturnsToView()
    {
        Arrange();
        string? saved = null;
        var field = Render("## Hi", onSave: text =>
        {
            saved = text;
            return System.Threading.Tasks.Task.FromResult(true);
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        field.Find("textarea").Blur();

        Assert.Equal("## Bye", saved);
        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void LeavingWithoutAChangeReturnsToViewWithoutSaving()
    {
        Arrange();
        var saveCalled = false;
        var field = Render("## Hi", onSave: _ =>
        {
            saveCalled = true;
            return System.Threading.Tasks.Task.FromResult(true);
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Blur();

        Assert.False(saveCalled);
        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void EscapeDiscardsTheDraftAndReturnsToView()
    {
        Arrange();
        var saveCalled = false;
        var field = Render("## Hi", onSave: _ =>
        {
            saveCalled = true;
            return System.Threading.Tasks.Task.FromResult(true);
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        field.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(saveCalled);
        Assert.Empty(field.FindAll("textarea"));
        Assert.Contains(">Hi<", field.Markup);
    }

    [Fact]
    public void EscapeInTheBlankEditorClearsIt()
    {
        Arrange();
        var field = Render("");

        field.Find("textarea").Input("Typed");
        field.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("", field.Find("textarea").TextContent);
    }

    [Fact]
    public async Task DragSelectingTheRenderedTextKeepsTheView()
    {
        Arrange();
        var field = Render("## Hi");

        await field.Find(".pspad-markdown-content")
            .TriggerEventAsync("onmarkdownviewclick", new MarkdownViewClickEventArgs { HasSelection = true });

        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void ARejectedSaveRefocusesTheEditor()
    {
        Arrange();
        var field = Render("## Hi", onSave: _ => System.Threading.Tasks.Task.FromResult(false));

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        var before = FocusCalls();
        field.Find("textarea").Blur();

        Assert.True(FocusCalls() > before);
    }

    [Fact]
    public void ReadOnlyOffersNoEdit()
    {
        Arrange();

        var field = Render("## Hi", readOnly: true);
        Assert.Empty(field.FindAll(".pspad-markdown-edit"));
        Assert.Empty(field.FindAll(".pspad-markdown-content"));

        var blank = Render("", readOnly: true);
        Assert.Empty(blank.FindAll("textarea"));
        Assert.Contains("No description", blank.Markup);
    }

    [Fact]
    public void DisabledOffersNoEdit()
    {
        Arrange();

        var field = Render("## Hi", disabled: true);
        Assert.Empty(field.FindAll(".pspad-markdown-edit"));
        Assert.Empty(field.FindAll(".pspad-markdown-content"));

        var blank = Render("", disabled: true);
        Assert.Empty(blank.FindAll("textarea"));
        Assert.Contains("No description", blank.Markup);
    }

    [Fact]
    public void AThrowingSaveStaysInEditKeepingTheDraft()
    {
        Arrange();
        var field = Render("## Hi", onSave: _ => throw new InvalidOperationException("boom"));

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        field.Find("textarea").Blur();

        Assert.Equal("## Bye", field.Find("textarea").TextContent);
    }

    [Fact]
    public void ARejectedSaveStaysInEditKeepingTheDraft()
    {
        Arrange();
        var field = Render("## Hi", onSave: _ => System.Threading.Tasks.Task.FromResult(false));

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        field.Find("textarea").Blur();

        Assert.Equal("## Bye", field.Find("textarea").TextContent);
    }

    [Fact]
    public void ASaveInFlightIsNotSentTwice()
    {
        Arrange();
        var gate = new TaskCompletionSource<bool>();
        var calls = 0;
        var field = Render("## Hi", onSave: async _ =>
        {
            calls++;
            return await gate.Task;
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Bye");
        field.Find("textarea").Blur();
        field.Find("textarea").Blur();

        Assert.Equal(1, calls);
        gate.SetResult(true);
    }

    [Fact]
    public void RerenderingWithANewValueInViewModeShowsTheNewMarkup()
    {
        Arrange();
        var field = Render("## Hi");

        field.Render(parameters => parameters.Add(p => p.Value, "## Bye"));

        Assert.Contains("Bye", field.Markup);
        Assert.DoesNotContain(">Hi<", field.Markup);
    }

    [Fact]
    public void TheEditButtonAndTextareaCarryAriaLabels()
    {
        Arrange();
        var field = Render("## Hi");

        Assert.Equal("Edit description", field.Find(".pspad-markdown-edit").GetAttribute("aria-label"));

        field.Find(".pspad-markdown-edit").Click();

        Assert.Equal("Description", field.Find("textarea").GetAttribute("aria-label"));
    }

    [Fact]
    public void AnOutsideChangeWhileEditingKeepsTheDraft()
    {
        Arrange();
        var field = Render("## Hi");

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Input("## Typed");
        field.Render(parameters => parameters.Add(p => p.Value, "## Other"));

        Assert.Equal("## Typed", field.Find("textarea").TextContent);
    }

    [Fact]
    public void AnOutsideChangeWhileTypingIntoTheBlankEditorKeepsTheDraft()
    {
        Arrange();
        var field = Render("");

        field.Find("textarea").Input("Typed");
        field.Render(parameters => parameters.Add(p => p.Value, "Other"));

        Assert.Equal("Typed", field.Find("textarea").TextContent);
    }

    int FocusCalls() => JSInterop.Invocations.Count(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

    static System.Threading.Tasks.Task ClickRenderedText(IRenderedComponent<MarkdownField> field, bool onLink) =>
        field.Find(".pspad-markdown-content")
            .TriggerEventAsync("onmarkdownviewclick", new MarkdownViewClickEventArgs { OnLink = onLink });

    IRenderedComponent<MarkdownField> Render(
        string value,
        Func<string, System.Threading.Tasks.Task<bool>>? onSave = null,
        bool readOnly = false,
        bool disabled = false) =>
        Render<MarkdownField>(parameters => parameters
            .Add(p => p.Value, value)
            .Add(p => p.OnSave, onSave ?? (_ => System.Threading.Tasks.Task.FromResult(true)))
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.Disabled, disabled));

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}
