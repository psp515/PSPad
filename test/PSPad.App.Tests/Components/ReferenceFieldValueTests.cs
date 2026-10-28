using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ReferenceFieldValueTests : Bunit.TestContext
{
    [Fact]
    public void ATextFieldRendersAsPlainText()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Colour", "black", null, 0));

        Assert.Equal("black", field.Text().Trim());
        Assert.Empty(field.FindAll("a"));
    }

    [Fact]
    public void ALinkFieldRendersAsAnOpenInNewTabLink()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Source", "https://onedrive.live.com/x", null, 0));

        var link = field.Find("a");
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("https://onedrive.live.com/x", link.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
    }

    [Fact]
    public void AnUnsafeSchemeNeverBecomesAnHref()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Source", "javascript:alert(1)", "link", 0));

        Assert.Empty(field.FindAll("a"));
        Assert.Contains("javascript:alert(1)", field.Text());
    }

    [Fact]
    public void APathFieldRendersAsCodeWithACopyButton()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));

        Assert.Equal("/etc", field.Find("code").TextContent);
        Assert.NotEmpty(field.FindAll("button[aria-label='Copy']"));
    }

    [Fact]
    public void TheCopyButtonCopiesTheValueAndShowsASnackbar()
    {
        Arrange();
        JSInterop.SetupModule("./js/clipboard.js").SetupVoid("copy", "/etc").SetVoidResult();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));
        field.Find("button[aria-label='Copy']").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Copied") == true);
    }

    [Fact]
    public void AFailedCopyShowsAFallbackSnackbar()
    {
        Arrange();
        JSInterop.SetupModule("./js/clipboard.js").SetupVoid("copy", "/etc").SetException(new JSException("denied"));

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));
        field.Find("button[aria-label='Copy']").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            snack => snack.Message?.Contains("Could not copy") == true);
    }

    [Fact]
    public void AQuantityFieldNormalisesTheSpacing()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Weight", "350g", null, 0));

        Assert.Equal("350 g", field.Text().Trim());
    }

    [Fact]
    public void SaveIsDisabledWithABlankLabel()
    {
        Arrange();

        var editor = RenderEditor(null);

        Assert.True(Button(editor, "Save").HasAttribute("disabled"));
    }

    [Fact]
    public void SavingHandsBackTheChosenQuantityKind()
    {
        Arrange();
        (string Label, string Value, string? Display)? saved = null;

        var editor = RenderEditor(null, onSave: tuple =>
        {
            saved = tuple;
            return Task.CompletedTask;
        });

        editor.Labelled("Label").Change("Left");
        editor.Labelled("Value").Change("350 g");
        editor.Find("[role=combobox]").MouseDown();
        editor.FindAll("[role=option]").Single(option => option.TextContent.Trim() == "Quantity").Click();

        Button(editor, "Save").Click();

        Assert.Equal(("Left", "350 g", "quantity"), saved);
    }

    [Fact]
    public void AutomaticIsTheDefaultAndSavesANullHint()
    {
        Arrange();
        (string Label, string Value, string? Display)? saved = null;

        var editor = RenderEditor(null, onSave: tuple =>
        {
            saved = tuple;
            return Task.CompletedTask;
        });

        editor.Labelled("Label").Change("Left");
        editor.Labelled("Value").Change("350 g");
        Button(editor, "Save").Click();

        Assert.Equal(("Left", "350 g", (string?)null), saved);
    }

    [Fact]
    public void RemoveIsHiddenForANewField()
    {
        Arrange();

        var editor = RenderEditor(null);

        Assert.DoesNotContain(editor.FindAll("button"), button => button.TextContent.Trim() == "Remove");
    }

    [Fact]
    public void RemoveIsOfferedForAnExistingField()
    {
        Arrange();

        var editor = RenderEditor(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));

        Assert.NotNull(Button(editor, "Remove"));
    }

    [Fact]
    public void DroppingAUriListFillsTheEditorsValueInput()
    {
        Arrange();
        var editor = RenderEditor(null);
        var component = editor.FindComponent<ReferenceFieldEditor>();

        editor.InvokeAsync(() => component.Instance.OnDropped("https://example.com/x", null, false));
        editor.Render();

        Assert.Equal("https://example.com/x", editor.Labelled("Value").TextContent);
    }

    [Fact]
    public void DroppingAFileWithNoUsableTextShowsAnInfoSnackbarInTheEditor()
    {
        Arrange();
        var editor = RenderEditor(null);
        var component = editor.FindComponent<ReferenceFieldEditor>();

        editor.InvokeAsync(() => component.Instance.OnDropped(null, null, true));
        editor.Render();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            snack => snack.Message?.Contains("Browsers can't read a file's full path") == true
                     && snack.Severity == Severity.Info);
    }

    static AngleSharp.Dom.IElement Button(IRenderedComponent<IComponent> host, string text) =>
        host.FindAll("button").Single(button => button.TextContent.Trim() == text);

    IRenderedComponent<ReferenceFieldValue> Render(ReferenceField field) =>
        Render<ReferenceFieldValue>(parameters => parameters.Add(p => p.Field, field));

    IRenderedComponent<ContainerFragment> RenderEditor(
        ReferenceField? field,
        Func<(string Label, string Value, string? Display), Task>? onSave = null,
        Func<Task>? onRemove = null) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ReferenceFieldEditor>(1);
            builder.AddAttribute(2, nameof(ReferenceFieldEditor.Field), field);
            builder.AddAttribute(3, nameof(ReferenceFieldEditor.OnSave), onSave ?? (_ => Task.CompletedTask));
            builder.AddAttribute(4, nameof(ReferenceFieldEditor.OnRemove), onRemove ?? (() => Task.CompletedTask));
            builder.CloseComponent();
        });

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
        Services.AddScoped<Clipboard>();
    }
}
