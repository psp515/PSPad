using Bunit;
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
        fields.Find(".pspad-field-add-label input").Input("Time");
        fields.Find(".pspad-field-add-value input").Input("45 min");
        fields.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        var field = Assert.Single(stored!.Fields);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Null(field.Display);
        Assert.Equal("", fields.Find(".pspad-field-add-label input").GetAttribute("value") ?? "");
        Assert.Equal("", fields.Find(".pspad-field-add-value input").GetAttribute("value") ?? "");
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public async Task EnterInTheLabelFieldMovesFocusToValueWithoutAdding()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-add-label input").Input("Servings");
        fields.Find(".pspad-field-add-label input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
        Assert.Equal("Servings", fields.Find(".pspad-field-add-label input").GetAttribute("value"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public async Task LeavingTheValueWithBothFilledAddsNothing()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-add-label input").Input("Time");
        fields.Find(".pspad-field-add-value input").Input("45 min");
        fields.Find(".pspad-field-add-value input").Blur();

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
    }

    [Fact]
    public async Task EnterWithNoLabelAddsNothing()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-add-value input").Input("45 min");
        fields.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == FocusIdentifier);
    }

    [Fact]
    public void ARejectedAddSurfacesTheRejectionAndKeepsTheText()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        Services.AddSingleton<ICommandHandler<AddReferenceField>>(
            new RejectingHandler<AddReferenceField>("Item no longer exists."));

        var fields = RenderList(item);
        fields.Find(".pspad-field-add-label input").Input("Time");
        fields.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Item no longer exists.") == true);
        Assert.Equal("Time", fields.Find(".pspad-field-add-label input").GetAttribute("value"));
    }

    [Fact]
    public void ARejectedMoveDoesNotReportAChange()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AppTestHost.Arrange(this, User, Today, item);
        Services.AddSingleton<ICommandHandler<MoveReferenceField>>(
            new RejectingHandler<MoveReferenceField>("Field no longer exists."));
        var changes = 0;

        var fields = Render<ReferenceFieldList>(parameters => parameters
            .Add(p => p.Item, item).Add(p => p.UserId, User).Add(p => p.Changed, () => changes++));
        fields.FindAll(".pspad-field-down")[0].Click();

        Assert.Equal(0, changes);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Field no longer exists.") == true);
    }

    [Fact]
    public void CopyingAPathDoesNotOpenTheEditor()
    {
        var item = NewItem();
        AddField(item, "Folder", "/srv/prints");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-copy").Click();

        Assert.Empty(fields.FindAll(".pspad-field-editor"));
        Assert.NotEmpty(fields.FindAll(".pspad-field-row"));
    }

    [Fact]
    public void FollowingALinkDoesNotOpenTheEditor()
    {
        var item = NewItem();
        AddField(item, "Site", "https://example.com");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find("a.pspad-field-link").Click();

        Assert.Empty(fields.FindAll(".pspad-field-editor"));
    }

    [Fact]
    public void FieldRowsShareTheAddRowsLeadColumn()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);

        Assert.NotNull(fields.Find(".pspad-field-row").QuerySelector(".pspad-step-lead"));
    }

    [Fact]
    public async Task TheRowRemoveButtonDropsTheField()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        var remove = fields.Find(".pspad-field-remove");
        Assert.Equal("Remove Time", remove.GetAttribute("aria-label"));
        remove.Click();

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(stored!.Fields);
    }

    [Fact]
    public void ARejectedRemoveSurfacesTheRejection()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);
        Services.AddSingleton<ICommandHandler<RemoveReferenceField>>(
            new RejectingHandler<RemoveReferenceField>("Field no longer exists."));

        var fields = RenderList(item);
        fields.Find(".pspad-field-remove").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Field no longer exists.") == true);
    }

    [Fact]
    public void EachRowShowsItsLabelAboveItsValue()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);

        var row = fields.Find(".pspad-field-row");
        Assert.Equal("Time", row.QuerySelector(".pspad-field-label")!.TextContent.Trim());
        Assert.Contains("45 min", row.QuerySelector(".pspad-field-row-body")!.TextContent);
    }

    [Fact]
    public void ClickingARowOpensItsEditorInPlace()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-row-body").Click();

        Assert.Empty(fields.FindAll(".pspad-field-row"));
        Assert.Equal("Time", fields.Find(".pspad-field-label-input input").GetAttribute("value"));
    }

    [Fact]
    public void DisabledOffersNoAddOrRemove()
    {
        var item = NewItem();
        AddField(item, "Time", "45 min");
        AppTestHost.Arrange(this, User, Today, item);

        var fields = Render<ReferenceFieldList>(parameters => parameters
            .Add(p => p.Item, item).Add(p => p.UserId, User).Add(p => p.Disabled, true));

        Assert.True(fields.Find(".pspad-field-remove").HasAttribute("disabled"));
        Assert.True(fields.Find(".pspad-field-add-label input").HasAttribute("disabled"));
    }

    [Fact]
    public async Task WithoutAnItemEnterAddsToTheDraftAndSendsNothing()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField>();
        var outbox = Services.GetRequiredService<PSPad.App.State.Outbox.IOutbox>();

        var fields = RenderDraft(draft);
        fields.Find(".pspad-field-add-label input").Input("Time");
        fields.Find(".pspad-field-add-value input").Input("45 min");
        fields.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var field = Assert.Single(draft);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Null(field.Display);
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal("Time", fields.Find(".pspad-field-row .pspad-field-label").TextContent.Trim());
        Assert.Equal("", fields.Find(".pspad-field-add-label input").GetAttribute("value") ?? "");
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
        fields.FindAll(".pspad-field-up")[2].Click();

        Assert.Equal(["Time", "Difficulty", "Servings"], draft.Select(field => field.Label));
        Assert.Equal([0, 1, 2], draft.Select(field => field.Position));
        Assert.Equal(["Time", "Difficulty", "Servings"],
            fields.FindAll(".pspad-field-row .pspad-field-label").Select(label => label.TextContent.Trim()));
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
        fields.FindAll(".pspad-field-remove")[0].Click();

        Assert.Equal(["Servings"], draft.Select(field => field.Label));
        Assert.Equal(0, draft[0].Position);
        Assert.Single(fields.FindAll(".pspad-field-row"));
    }

    [Fact]
    public void WithoutAnItemEditingRewritesTheDraftField()
    {
        AppTestHost.Arrange(this, User, Today);
        var draft = new List<ReferenceField> { new(Guid.NewGuid(), "Time", "45 min", null, 0) };

        var fields = RenderDraft(draft);
        fields.Find(".pspad-field-row-body").Click();
        fields.Find(".pspad-field-label-input input").Change("Cooking time");
        fields.Find(".pspad-field-save").Click();

        var field = Assert.Single(draft);
        Assert.Equal("Cooking time", field.Label);
        Assert.Equal("45 min", field.Value);
        Assert.Equal("Cooking time", fields.Find(".pspad-field-row .pspad-field-label").TextContent.Trim());
    }

    [Fact]
    public void DroppingAUriListFillsTheValueInput()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped("https://example.com/x", null, false));
        fields.Render();

        Assert.Equal("https://example.com/x", fields.Find(".pspad-field-add-value input").GetAttribute("value"));
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

        Assert.Equal("https://example.com/first", fields.Find(".pspad-field-add-value input").GetAttribute("value"));
    }

    [Fact]
    public void DroppingPlainTextFillsTheValueInput()
    {
        var item = NewItem();
        AppTestHost.Arrange(this, User, Today, item);
        var fields = RenderList(item);

        fields.InvokeAsync(() => fields.Instance.OnDropped(null, "OneDrive/recipes.txt", false));
        fields.Render();

        Assert.Equal("OneDrive/recipes.txt", fields.Find(".pspad-field-add-value input").GetAttribute("value"));
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
        Assert.Equal("", fields.Find(".pspad-field-add-value input").GetAttribute("value") ?? "");
    }

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
