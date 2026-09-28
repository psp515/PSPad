using Bunit;
using MudBlazor.Services;
using PSPad.App.Components;
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
    public void ABlankValueShowsThePlaceholder()
    {
        Arrange();

        var field = Render("", placeholder: "Add a description");

        Assert.Contains("Add a description", field.Markup);
    }

    [Fact]
    public void EditSwitchesToTheRawText()
    {
        Arrange();
        var field = Render("## Hi");

        field.Find(".pspad-markdown-edit").Click();

        Assert.Equal("## Hi", field.Find("textarea").TextContent);
    }

    [Fact]
    public void SavingHandsTheTextBackAndReturnsToView()
    {
        Arrange();
        string? saved = null;
        var field = Render("## Hi", onSave: text =>
        {
            saved = text;
            return System.Threading.Tasks.Task.CompletedTask;
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Change("## Bye");
        field.Find(".pspad-markdown-save").Click();

        Assert.Equal("## Bye", saved);
        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void CancelDiscardsTheDraft()
    {
        Arrange();
        var saveCalled = false;
        var field = Render("## Hi", onSave: _ =>
        {
            saveCalled = true;
            return System.Threading.Tasks.Task.CompletedTask;
        });

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Change("## Bye");
        field.Find(".pspad-markdown-cancel").Click();

        Assert.False(saveCalled);
        Assert.Empty(field.FindAll("textarea"));
    }

    [Fact]
    public void ReadOnlyOffersNoEdit()
    {
        Arrange();

        var field = Render("## Hi", readOnly: true);

        Assert.Empty(field.FindAll(".pspad-markdown-edit"));
    }

    [Fact]
    public void AnOutsideChangeWhileEditingKeepsTheDraft()
    {
        Arrange();
        var field = Render("## Hi");

        field.Find(".pspad-markdown-edit").Click();
        field.Find("textarea").Change("## Typed");
        field.Render(parameters => parameters.Add(p => p.Value, "## Other"));

        Assert.Equal("## Typed", field.Find("textarea").TextContent);
    }

    IRenderedComponent<MarkdownField> Render(
        string value,
        Func<string, System.Threading.Tasks.Task>? onSave = null,
        bool readOnly = false,
        bool disabled = false,
        string placeholder = "Add a description") =>
        Render<MarkdownField>(parameters => parameters
            .Add(p => p.Value, value)
            .Add(p => p.OnSave, onSave ?? (_ => System.Threading.Tasks.Task.CompletedTask))
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.Disabled, disabled)
            .Add(p => p.Placeholder, placeholder));

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}
