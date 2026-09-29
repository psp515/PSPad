using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ReferenceItemPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public async Task ANewItemIsCreatedInTheList()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var replica = AppTestHost.Arrange(this, User, Today, list);
        var navigation = Services.GetRequiredService<NavigationManager>();

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        panel.Find(".pspad-panel-save").Click();

        var items = await replica.LoadAllAsync<ReferenceItem>(User);
        var created = Assert.Single(items);
        Assert.Equal(list.Id, created.ListId);
        Assert.Equal("Bigos", created.Name);
        Assert.Contains($"item={created.Id}", navigation.Uri);
    }

    [Fact]
    public void ItShowsTheNameThenTheFieldsSectionThenTheDescriptionSection()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        Describe(item, "Polish stew");
        AddField(item, "Time", "45 min");
        var secondFieldId = AddField(item, "Servings", "4");
        item.ApplyAll(ReferenceItem.Decide(
            item, new MoveReferenceField(Guid.NewGuid(), User, item.Id, secondFieldId, 0), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        var sections = panel.FindAll(".pspad-panel-section");
        Assert.Equal(["Fields", "Description"],
            sections.Select(section => section.QuerySelector(".pspad-panel-section-title")!.TextContent.Trim()));
        Assert.Equal(["Servings", "Time"],
            sections[0].QuerySelectorAll(".pspad-field-label").Select(label => label.TextContent.Trim()));
        Assert.Contains("Polish stew", sections[1].TextContent);
        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-item-name-field", StringComparison.Ordinal)
            < markup.IndexOf("pspad-panel-section", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenamingSendsRenameReferenceItem()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-item-name-field input").Change("Bigos myśliwski");

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal("Bigos myśliwski", reloaded!.Name);
    }

    [Fact]
    public async Task StarringSendsStarReferenceItem()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-item-star").Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.True(reloaded!.Starred);
    }

    [Fact]
    public async Task SavingTheDescriptionSendsSetReferenceItemDescription()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-markdown-input textarea").Input("Slow cooked");
        panel.Find(".pspad-markdown-input textarea").Blur();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal("Slow cooked", reloaded!.Description);
    }

    [Fact]
    public async Task AddingAFieldFromTheAddRowSendsAddReferenceFieldAndShowsIt()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-field-add-label input").Input("Time");
        panel.Find(".pspad-field-add-value input").Input("45 min");
        panel.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        var field = Assert.Single(reloaded!.Fields);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
        panel.WaitForAssertion(() => Assert.Single(panel.FindAll(".pspad-field-row")));
    }

    [Fact]
    public void TheOldAddFieldButtonIsGone()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        Assert.Empty(panel.FindAll(".pspad-field-add"));
        Assert.DoesNotContain("Add field", panel.Markup);
    }

    [Fact]
    public async Task EditingAFieldSendsEditReferenceFieldWithItsHint()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AddField(item, "Site", "https://example.com");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = RenderWithOverlays(itemId: item.Id);
        panel.Find(".pspad-field-row-body").Click();
        panel.Find(".pspad-field-kind-input .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            option => option.TextContent.Trim() == "Path"));
        panel.FindAll(".mud-list-item").First(option => option.TextContent.Trim() == "Path").Click();
        panel.Find(".pspad-field-save").Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        var field = Assert.Single(reloaded!.Fields);
        Assert.Equal("path", field.Display);
    }

    [Fact]
    public async Task RemovingAFieldSendsRemoveReferenceField()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AddField(item, "Time", "45 min");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-field-row-body").Click();
        panel.Find(".pspad-field-editor-remove").Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Empty(reloaded!.Fields);
    }

    [Fact]
    public async Task MovingAFieldDownSendsMoveReferenceField()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.FindAll(".pspad-field-down")[0].Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal("Servings", reloaded!.Fields[0].Label);
        Assert.Equal("Time", reloaded.Fields[1].Label);
    }

    [Fact]
    public async Task MovingTheLastFieldUpSendsToIndexOneOfThree()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AddField(item, "Difficulty", "Easy");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.FindAll(".pspad-field-up")[2].Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal(["Time", "Difficulty", "Servings"], reloaded!.Fields.Select(field => field.Label));
    }

    [Fact]
    public async Task MovingTheFirstFieldDownSendsToIndexOneOfThree()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AddField(item, "Time", "45 min");
        AddField(item, "Servings", "4");
        AddField(item, "Difficulty", "Easy");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.FindAll(".pspad-field-down")[0].Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal(["Servings", "Time", "Difficulty"], reloaded!.Fields.Select(field => field.Label));
    }

    [Fact]
    public void ANewItemNavigatesWithReplaceSoBackDoesNotReturnToAddMode()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        AppTestHost.Arrange(this, User, Today, list);
        var navigation = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        panel.Find(".pspad-panel-save").Click();

        var entry = Assert.Single(navigation.History);
        Assert.True(entry.Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void ARejectedStarStaysUnstarredAndSurfacesTheRejection()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AppTestHost.Arrange(this, User, Today, list, item);
        Services.AddSingleton<ICommandHandler<StarReferenceItem>>(
            new RejectingHandler<StarReferenceItem>("Item no longer exists."));

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-item-star").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Item no longer exists.") == true);
    }

    [Fact]
    public void ADeletedItemShowsAsMissingAndTheReloadedPanelIsClosed()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        item.ApplyAll(ReferenceItem.Decide(
            item, new DeleteReferenceItem(Guid.NewGuid(), User, item.Id), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        Assert.Empty(panel.FindAll(".pspad-item-name-field"));
    }

    [Fact]
    public void TheMovePickerOffersReferenceListsOnly()
    {
        var referenceList = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var otherReferenceList = NewReferenceList(Guid.NewGuid(), "Notatki");
        var taskList = NewTaskList(Guid.NewGuid(), "Zakupy");
        var item = NewItem(referenceList.Id, "Bigos");
        AppTestHost.Arrange(this, User, Today, referenceList, otherReferenceList, taskList, item);

        var panel = RenderWithOverlays(itemId: item.Id);
        panel.Find(".pspad-item-move .mud-select-input").MouseDown();

        panel.WaitForAssertion(() =>
        {
            var options = panel.FindAll(".mud-list-item").Select(option => option.TextContent.Trim()).ToArray();
            Assert.Contains("Przepisy", options);
            Assert.Contains("Notatki", options);
            Assert.DoesNotContain("Zakupy", options);
        });
    }

    [Fact]
    public async Task DeletingAsksFirstThenSendsDeleteReferenceItem()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = RenderWithOverlays(itemId: item.Id);
        panel.Find(".pspad-item-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.True(reloaded!.Deleted);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid? itemId = null, Guid? newInList = null) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ReferenceItemPanel>(2);
        builder.AddAttribute(3, nameof(ReferenceItemPanel.ItemId), itemId);
        builder.AddAttribute(4, nameof(ReferenceItemPanel.NewInList), newInList);
        builder.CloseComponent();
    });

    static TaskList NewReferenceList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0, ListKind.Reference),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList NewTaskList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static ReferenceItem NewItem(Guid listId, string name)
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), listId, name, 0),
            DateTimeOffset.UnixEpoch));
        return item;
    }

    static void Describe(ReferenceItem item, string description) =>
        item.ApplyAll(ReferenceItem.Decide(
            item, new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, description), DateTimeOffset.UnixEpoch));

    static Guid AddField(ReferenceItem item, string label, string value)
    {
        var fieldId = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, label, value, null),
            DateTimeOffset.UnixEpoch));
        return fieldId;
    }
}
