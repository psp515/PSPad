using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ReferenceFieldListTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 29);
    const string FocusIdentifier = "Blazor._internal.domWrapper.focus";

    [Fact]
    public async Task EnterInTheValueFieldAddsAFieldAndClearsTheAddRow()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        LabelField(fields).Input("Time");
        ValueField(fields).Input("45 min");
        ValueField(fields).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        var field = Assert.Single(stored!.Fields);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Null(field.Display);
        Assert.Equal("", LabelField(fields).GetAttribute("value") ?? "");
        Assert.Equal("", ValueField(fields).GetAttribute("value") ?? "");
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public void TheAddRowStopsAtTheLabelAndValueLimits()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);

        Assert.Equal($"{ContentLimits.Label}", LabelField(fields).GetAttribute("maxlength"));
        Assert.Equal($"{ContentLimits.FieldValue}", ValueField(fields).GetAttribute("maxlength"));
    }

    [Fact]
    public async Task EnterInTheLabelFieldMovesFocusToValueWithoutAdding()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        LabelField(fields).Input("Servings");
        LabelField(fields).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
        Assert.Equal("Servings", LabelField(fields).GetAttribute("value"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public async Task LeavingTheValueWithBothFilledAddsNothing()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        LabelField(fields).Input("Time");
        ValueField(fields).Input("45 min");
        ValueField(fields).Blur();

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
    }

    [Fact]
    public async Task EnterWithNoLabelAddsNothing()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        ValueField(fields).Input("45 min");
        ValueField(fields).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public void ARejectedAddSurfacesTheRejectionAndKeepsTheText()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today);

        var fields = RenderList(item);
        LabelField(fields).Input("Time");
        ValueField(fields).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Severity == Severity.Warning);
        Assert.Equal("Time", LabelField(fields).GetAttribute("value"));
    }

    [Fact]
    public void ARejectedMoveDoesNotReportAChange()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AppTestHost.Arrange(this, User, Today);
        var changes = 0;

        var fields = Render<ReferenceFieldList>(parameters => parameters
            .Add(p => p.Item, item).Add(p => p.UserId, User).Add(p => p.Changed, () => changes++));
        Drag.Drop(fields, item.Fields[0], 1);

        Assert.Equal(0, changes);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Severity == Severity.Warning);
    }

    [Fact]
    public void CopyingAPathDoesNotOpenTheEditor()
    {
        var item = NewItem();
        AddField(item, "Folder", "/srv/prints");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find("button[aria-label='Copy']").Click();

        Assert.False(EditorIsOpen(fields));
        Assert.Contains("/srv/prints", fields.Text());
    }

    [Fact]
    public void FollowingALinkDoesNotOpenTheEditor()
    {
        var item = NewItem();
        AddField(item, "Site", "https://example.com");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find("a").Click();

        Assert.False(EditorIsOpen(fields));
    }

    [Fact]
    public async Task TheRowRemoveButtonDropsTheField()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find("button[aria-label='Remove Time']").Click();

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
    }

    [Fact]
    public void ARejectedRemoveSurfacesTheRejection()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today);

        var fields = RenderList(item);
        fields.Find("button[aria-label='Remove Time']").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Severity == Severity.Warning);
    }

    [Fact]
    public void EachRowShowsItsLabelAboveItsValue()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);

        var text = Squash(fields.Text());
        Assert.True(text.IndexOf("Time", StringComparison.Ordinal) < text.IndexOf("45min", StringComparison.Ordinal));
    }

    [Fact]
    public void ClickingARowOpensItsEditorInPlace()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        OpenRow(fields, "Time");

        Assert.True(EditorIsOpen(fields));
        Assert.Equal("Time", fields.Labelled("Label").GetAttribute("value"));
    }

    [Fact]
    public void DisabledOffersNoAddOrRemove()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = Render<ReferenceFieldList>(parameters => parameters
            .Add(p => p.Item, item).Add(p => p.UserId, User).Add(p => p.Disabled, true));

        Assert.True(fields.Find("button[aria-label='Remove Time']").HasAttribute("disabled"));
        Assert.True(LabelField(fields).HasAttribute("disabled"));
    }

    [Fact]
    public async Task WithoutAnItemEnterAddsToTheDraftAndSendsNothing()
    {
        var replica = AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField>();

        var fields = RenderDraft(draft);
        LabelField(fields).Input("Time");
        ValueField(fields).Input("45 min");
        ValueField(fields).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var field = Assert.Single(draft);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Null(field.Display);
        Assert.Empty(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.NotNull(fields.Find("button[aria-label='Remove Time']"));
        Assert.Equal("", LabelField(fields).GetAttribute("value") ?? "");
    }

    [Fact]
    public void WithoutAnItemMovingReordersTheDraft()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField>
        {
            new(Guid.NewGuid(), "Time", "45 min", null, 0),
            new(Guid.NewGuid(), "Servings", "4", null, 1),
            new(Guid.NewGuid(), "Difficulty", "Easy", null, 2)
        };

        var fields = RenderDraft(draft);
        Drag.Drop(fields, draft[2], 1);

        Assert.Equal(["Time", "Difficulty", "Servings"], draft.Select(field => field.Label));
        Assert.Equal([0, 1, 2], draft.Select(field => field.Position));
        Assert.Equal(["Remove Time", "Remove Difficulty", "Remove Servings"], RemoveLabels(fields));
    }

    [Fact]
    public void EveryFieldEndsInADragHandle()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = Render<ReferenceFieldList>(parameters => parameters.Add(p => p.Item, item).Add(p => p.UserId, User));

        Assert.Equal(["Drag Time", "Drag Servings"],
            fields.FindAll("[aria-label^='Drag ']").Select(handle => handle.GetAttribute("aria-label")));
    }

    [Fact]
    public void WithoutAnItemRemovingDropsItFromTheDraft()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField>
        {
            new(Guid.NewGuid(), "Time", "45 min", null, 0),
            new(Guid.NewGuid(), "Servings", "4", null, 1)
        };

        var fields = RenderDraft(draft);
        fields.Find("button[aria-label='Remove Time']").Click();

        Assert.Equal(["Servings"], draft.Select(field => field.Label));
        Assert.Equal(0, draft[0].Position);
        Assert.Equal(["Remove Servings"], RemoveLabels(fields));
    }

    [Fact]
    public void WithoutAnItemEditingRewritesTheDraftField()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField> { new(Guid.NewGuid(), "Time", "45 min", null, 0) };

        var fields = RenderDraft(draft);
        OpenRow(fields, "Time");
        fields.Labelled("Label").Change("Cooking time");
        fields.FindAll("button").Single(button => button.TextContent.Trim() == "Save").Click();

        var field = Assert.Single(draft);
        Assert.Equal("Cooking time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Equal(["Remove Cooking time"], RemoveLabels(fields));
    }

    [Fact]
    public void DroppingAUriListFillsTheValueInput()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped("https://example.com/x", null, false));
        fields.Render();

        Assert.Equal("https://example.com/x", ValueField(fields).GetAttribute("value"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public void DroppingAUriListIgnoresCommentLinesAndTakesTheFirstUri()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped(
            "# a comment\nhttps://example.com/first\nhttps://example.com/second", null, false));
        fields.Render();

        Assert.Equal("https://example.com/first", ValueField(fields).GetAttribute("value"));
    }

    [Fact]
    public void DroppingPlainTextFillsTheValueInput()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped(null, "OneDrive/recipes.txt", false));
        fields.Render();

        Assert.Equal("OneDrive/recipes.txt", ValueField(fields).GetAttribute("value"));
    }

    [Fact]
    public void DroppingAFileWithNoUsableTextShowsAnInfoSnackbarAndLeavesTheValueEmpty()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped(null, null, true));
        fields.Render();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            snack => snack.Message?.Contains("Browsers can't read a file's full path") == true
                     && snack.Severity == Severity.Info);
        Assert.Equal("", ValueField(fields).GetAttribute("value") ?? "");
    }

    static IElement LabelField(IRenderedComponent<IComponent> fields) => fields.Find("input[aria-label='New label']");

    static IElement ValueField(IRenderedComponent<IComponent> fields) => fields.Find("input[aria-label='New label value']");

    static string[] RemoveLabels(IRenderedComponent<IComponent> fields) =>
        [.. fields.FindAll("button[aria-label^='Remove ']").Select(button => button.GetAttribute("aria-label")!)];

    static bool EditorIsOpen(IRenderedComponent<IComponent> fields) =>
        fields.FindAll("button").Any(button => button.TextContent.Trim() == "Save");

    static void OpenRow(IRenderedComponent<IComponent> fields, string label) =>
        fields.FindAll("span").First(span => span.Children.Length == 0 && span.TextContent.Trim() == label).Click();

    static string Squash(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    IRenderedComponent<ReferenceFieldList> RenderList(ReferenceItem item) =>
        Render<ReferenceFieldList>(parameters => parameters.Add(p => p.Item, item).Add(p => p.UserId, User));

    IRenderedComponent<ReferenceFieldList> RenderDraft(List<ReferenceField> draft) =>
        Render<ReferenceFieldList>(parameters => parameters.Add(p => p.Draft, draft).Add(p => p.UserId, User));

    static ReferenceItem NewItem()
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Bigos", 0),
            DateTimeOffset.UnixEpoch));
        return item;
    }

    static void AddField(ReferenceItem item, string label, string value) =>
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), label, value, null),
            DateTimeOffset.UnixEpoch));
}
