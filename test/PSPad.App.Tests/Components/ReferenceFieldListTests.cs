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
    }

    [Fact]
    public async Task EnterInTheLabelFieldAddsAField()
    {
        var item = NewItem();
        var replica = AppTestHost.Arrange(this, User, Today, item);

        var fields = RenderList(item);
        fields.Find(".pspad-field-add-label input").Input("Servings");
        fields.Find(".pspad-field-add-label input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal("Servings", Assert.Single(stored!.Fields).Label);
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
        fields.Find(".pspad-field-add-label input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

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

    IRenderedComponent<ReferenceFieldList> RenderList(ReferenceItem item) =>
        Render<ReferenceFieldList>(parameters => parameters.Add(p => p.Item, item).Add(p => p.UserId, User));

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
