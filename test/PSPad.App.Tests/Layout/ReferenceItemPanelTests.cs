using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Layout;
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
    public void ItShowsNameDescriptionAndFieldsInOrder()
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

        Assert.Contains("Bigos", panel.Markup);
        Assert.Contains("Polish stew", panel.Markup);
        var servingsIndex = panel.Markup.IndexOf("Servings", StringComparison.Ordinal);
        var timeIndex = panel.Markup.IndexOf("Time", StringComparison.Ordinal);
        Assert.True(servingsIndex >= 0 && timeIndex >= 0 && servingsIndex < timeIndex);
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
        panel.Find(".pspad-markdown-placeholder").Click();
        panel.Find("textarea").Change("Slow cooked");
        panel.Find(".pspad-markdown-save").Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal("Slow cooked", reloaded!.Description);
    }

    [Fact]
    public async Task AddingAFieldSendsAddReferenceField()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));
        panel.Find(".pspad-field-add").Click();
        panel.Find(".pspad-field-label-input input").Change("Time");
        panel.Find(".pspad-field-value-input textarea").Change("45 min");
        panel.Find(".pspad-field-save").Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        var field = Assert.Single(reloaded!.Fields);
        Assert.Equal("Time", field.Label);
        Assert.Equal("45 min", field.Value);
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
        panel.Find(".pspad-field-remove").Click();

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
