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

        Assert.Contains("pspad-field-text", field.Markup);
        Assert.Empty(field.FindAll("a"));
    }

    [Fact]
    public void ALinkFieldRendersAsAnOpenInNewTabLink()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Source", "https://onedrive.live.com/x", null, 0));

        var link = field.Find(".pspad-field-link");
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("https://onedrive.live.com/x", link.GetAttribute("href"));
    }

    [Fact]
    public void AnUnsafeSchemeNeverBecomesAnHref()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Source", "javascript:alert(1)", "link", 0));

        Assert.Empty(field.FindAll("a"));
        Assert.Contains("javascript:alert(1)", field.Markup);
    }

    [Fact]
    public void APathFieldRendersAsCodeWithACopyButton()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));

        var code = field.Find(".pspad-field-path");
        Assert.Equal("code", code.TagName.ToLowerInvariant());
        Assert.Equal("/etc", code.TextContent);
        Assert.NotEmpty(field.FindAll(".pspad-field-copy"));
    }

    [Fact]
    public void TheCopyButtonCopiesTheValueAndShowsASnackbar()
    {
        Arrange();
        JSInterop.SetupModule("./js/clipboard.js").SetupVoid("copy", "/etc").SetVoidResult();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));
        field.Find(".pspad-field-copy").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Copied") == true);
    }

    [Fact]
    public void AFailedCopyShowsAFallbackSnackbar()
    {
        Arrange();
        JSInterop.SetupModule("./js/clipboard.js").SetupVoid("copy", "/etc").SetException(new JSException("denied"));

        var field = Render(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));
        field.Find(".pspad-field-copy").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            snack => snack.Message?.Contains("Could not copy") == true);
    }

    [Fact]
    public void AQuantityFieldNormalisesTheSpacing()
    {
        Arrange();

        var field = Render(new ReferenceField(Guid.NewGuid(), "Weight", "350g", null, 0));

        var quantity = field.Find(".pspad-field-quantity");
        Assert.Equal("350 g", quantity.TextContent);
    }

    [Fact]
    public void SaveIsDisabledWithABlankLabel()
    {
        Arrange();

        var editor = RenderEditor(null);

        Assert.True(editor.Find(".pspad-field-save").HasAttribute("disabled"));
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

        editor.Find(".pspad-field-label-input input").Change("Left");
        editor.Find(".pspad-field-value-input textarea").Change("350 g");
        editor.Find(".pspad-field-kind-input .mud-select-input").MouseDown();
        editor.FindAll(".mud-list-item")[(int)FieldKind.Quantity + 1].Click();

        editor.Find(".pspad-field-save").Click();

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

        editor.Find(".pspad-field-label-input input").Change("Left");
        editor.Find(".pspad-field-value-input textarea").Change("350 g");
        editor.Find(".pspad-field-save").Click();

        Assert.Equal(("Left", "350 g", (string?)null), saved);
    }

    [Fact]
    public void RemoveIsHiddenForANewField()
    {
        Arrange();

        var editor = RenderEditor(null);

        Assert.Empty(editor.FindAll(".pspad-field-remove"));
    }

    [Fact]
    public void RemoveIsOfferedForAnExistingField()
    {
        Arrange();

        var editor = RenderEditor(new ReferenceField(Guid.NewGuid(), "Dir", "/etc", null, 0));

        Assert.NotEmpty(editor.FindAll(".pspad-field-remove"));
    }

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
        Services.AddScoped<Clipboard>();
    }
}
