using Bunit;
using System.Text.Json;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Areas;
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
        Assert.Equal(["Labels", "Description"],
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
    public void TheListRowOffersReferenceListsOnly()
    {
        var area = NewArea("Dom");
        var referenceList = NewReferenceList(area.Id, "Przepisy");
        var otherReferenceList = NewReferenceList(area.Id, "Notatki");
        var taskList = NewTaskList(area.Id, "Zakupy");
        var item = NewItem(referenceList.Id, "Bigos");
        AppTestHost.Arrange(this, User, Today, area, referenceList, otherReferenceList, taskList, item);

        var panel = RenderWithOverlays(itemId: item.Id);
        Assert.Contains("Dom › Przepisy", panel.Find(".pspad-task-list").TextContent);
        OpenRow(panel, ".pspad-task-list");

        var options = panel.FindAll(".pspad-list-option").Select(option => option.TextContent.Trim()).ToArray();
        Assert.Contains("Przepisy", options);
        Assert.Contains("Notatki", options);
        Assert.DoesNotContain("Zakupy", options);
        Assert.Empty(panel.FindAll(".pspad-item-move"));
    }

    [Fact]
    public async Task PickingAListInTheListRowMovesTheItem()
    {
        var home = NewArea("Dom");
        var work = NewArea("Praca");
        var recipes = NewReferenceList(home.Id, "Przepisy");
        var manuals = NewReferenceList(work.Id, "Instrukcje");
        var item = NewItem(recipes.Id, "Bigos");
        var replica = AppTestHost.Arrange(this, User, Today, home, work, recipes, manuals, item);

        var panel = RenderWithOverlays(itemId: item.Id);
        OpenRow(panel, ".pspad-task-list");
        panel.FindAll(".pspad-list-option").Single(option => option.TextContent.Contains("Instrukcje")).Click();

        var reloaded = await replica.LoadAsync<ReferenceItem>(item.Id);
        Assert.Equal(manuals.Id, reloaded!.ListId);
        panel.WaitForAssertion(() => Assert.Contains("Praca › Instrukcje", panel.Find(".pspad-task-list").TextContent));
    }

    [Fact]
    public void TheListRowSitsBetweenTheFieldsAndTheDescription()
    {
        var area = NewArea("Dom");
        var list = NewReferenceList(area.Id, "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        AppTestHost.Arrange(this, User, Today, area, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        var order = new[] { "pspad-item-name-field", "pspad-item-fields", "pspad-task-list", "pspad-item-description" }
            .Select(marker => panel.Markup.IndexOf(marker, StringComparison.Ordinal))
            .ToArray();
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
    }

    [Fact]
    public void TheFooterCarriesTheCreatedDateAndADeleteIcon()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos", new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero));
        AppTestHost.Arrange(this, User, Today, list, item);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        var footer = panel.Find(".pspad-item-footer");
        Assert.Equal("Created Wed, 23 Sep 2026", footer.QuerySelector(".mud-typography-caption")!.TextContent.Trim());
        Assert.NotNull(footer.QuerySelector(".pspad-item-delete"));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
    }

    [Fact]
    public void AnItemWithoutACreatedDateShowsNoCreatedText()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var item = NewItem(list.Id, "Bigos");
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(item))!.AsObject();
        json.Remove("CreatedAt");
        var legacy = json.Deserialize<ReferenceItem>()!;
        Assert.Null(legacy.CreatedAt);
        AppTestHost.Arrange(this, User, Today, list, legacy);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.ItemId, (Guid?)item.Id));

        Assert.DoesNotContain("Created", panel.Find(".pspad-item-footer").TextContent);
        Assert.NotNull(panel.Find(".pspad-item-delete"));
    }

    [Fact]
    public void ANewItemShowsTheSameSectionsAsAnExistingOne()
    {
        var area = NewArea("Dom");
        var list = NewReferenceList(area.Id, "Przepisy");
        AppTestHost.Arrange(this, User, Today, area, list);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));

        Assert.Equal(["Labels", "Description"],
            panel.FindAll(".pspad-panel-section").Select(section =>
                section.QuerySelector(".pspad-panel-section-title")!.TextContent.Trim()));
        Assert.Contains("Dom › Przepisy", panel.Find(".pspad-task-list").TextContent);
        Assert.NotNull(panel.Find(".pspad-field-add-label"));
        Assert.NotNull(panel.Find(".pspad-markdown-input"));
        Assert.Empty(panel.FindAll(".pspad-item-footer"));
    }

    [Fact]
    public async Task ANewItemIsCreatedWithItsDraftFieldsInOrderAndItsDescription()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var replica = AppTestHost.Arrange(this, User, Today, list);
        var navigation = Services.GetRequiredService<NavigationManager>();

        var panel = RenderWithOverlays(newInList: list.Id);
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        AddDraftField(panel, "Time", "45 min");
        AddDraftField(panel, "Site", "https://example.com");
        panel.FindAll(".pspad-field-row-body")[1].Click();
        panel.Find(".pspad-field-kind-input .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            option => option.TextContent.Trim() == "Path"));
        panel.FindAll(".mud-list-item").First(option => option.TextContent.Trim() == "Path").Click();
        panel.Find(".pspad-field-save").Click();
        panel.FindAll(".pspad-field-up")[1].Click();
        panel.Find(".pspad-markdown-input textarea").Input("Slow cooked");
        panel.Find(".pspad-markdown-input textarea").Blur();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.Equal("Bigos", created.Name);
        Assert.Equal(["Site", "Time"], created.Fields.Select(field => field.Label));
        Assert.Equal("path", created.Fields[0].Display);
        Assert.Equal("45 min", created.Fields[1].Value);
        Assert.Equal("Slow cooked", created.Description);
        Assert.Contains($"item={created.Id}", navigation.Uri);
    }

    [Fact]
    public async Task EnterInTheDraftFieldsAddRowDoesNotCreateTheItem()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        AddDraftField(panel, "Time", "45 min");

        Assert.Empty(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.Single(panel.FindAll(".pspad-field-row"));
    }

    [Fact]
    public async Task PickingAnotherListForANewItemCreatesItThere()
    {
        var area = NewArea("Dom");
        var recipes = NewReferenceList(area.Id, "Przepisy");
        var notes = NewReferenceList(area.Id, "Notatki");
        var replica = AppTestHost.Arrange(this, User, Today, area, recipes, notes);

        var panel = RenderWithOverlays(newInList: recipes.Id);
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        OpenRow(panel, ".pspad-task-list");
        panel.FindAll(".pspad-list-option").Single(option => option.TextContent.Contains("Notatki")).Click();
        panel.WaitForAssertion(() => Assert.Contains("Dom › Notatki", panel.Find(".pspad-task-list").TextContent));
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.Equal(notes.Id, created.ListId);
    }

    [Fact]
    public async Task ARejectedCreateWarnsAndKeepsTheDrafts()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var replica = AppTestHost.Arrange(this, User, Today, list);
        Services.AddSingleton<ICommandHandler<CreateReferenceItem>>(
            new RejectingHandler<CreateReferenceItem>("That list no longer exists."));
        var navigation = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        AddDraftField(panel, "Time", "45 min");
        panel.Find(".pspad-markdown-input textarea").Input("Slow cooked");
        panel.Find(".pspad-markdown-input textarea").Blur();
        panel.Find(".pspad-panel-save").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack =>
            snack.Message?.Contains("That list no longer exists.") == true && snack.Severity == Severity.Warning);
        Assert.Empty(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.Empty(navigation.History);
        Assert.Equal("Bigos", panel.Find(".pspad-item-name-field input").GetAttribute("value"));
        Assert.Single(panel.FindAll(".pspad-field-row"));
        Assert.Contains("Slow cooked", panel.Find(".pspad-item-description").TextContent);
    }

    [Fact]
    public async Task ARejectedFieldAfterCreateWarnsAndStillOpensTheItem()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var replica = AppTestHost.Arrange(this, User, Today, list);
        Services.AddSingleton<ICommandHandler<AddReferenceField>>(
            new RejectingHandler<AddReferenceField>("A field needs a label."));
        var navigation = Services.GetRequiredService<NavigationManager>();

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        AddDraftField(panel, "Time", "45 min");
        panel.Find(".pspad-panel-save").Click();

        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack =>
            snack.Message?.Contains("A field needs a label.") == true && snack.Severity == Severity.Warning);
        var created = Assert.Single(await replica.LoadAllAsync<ReferenceItem>(User));
        Assert.Contains($"item={created.Id}", navigation.Uri);
    }

    [Fact]
    public void ASecondAddWhileTheFirstIsInFlightSendsNothing()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        AppTestHost.Arrange(this, User, Today, list);
        var gate = new GatedHandler<CreateReferenceItem>();
        Services.AddSingleton<ICommandHandler<CreateReferenceItem>>(gate);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        panel.Find(".pspad-panel-save").Click();
        panel.WaitForAssertion(() => Assert.Equal(1, gate.Calls));
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        panel.Find(".pspad-item-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        gate.Open();

        panel.WaitForAssertion(() => Assert.Contains("item=", Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(1, gate.Calls);
    }

    [Fact]
    public void AnotherTargetListStartsAFreshDraft()
    {
        var list = NewReferenceList(Guid.NewGuid(), "Przepisy");
        var other = NewReferenceList(Guid.NewGuid(), "Notatki");
        AppTestHost.Arrange(this, User, Today, list, other);

        var panel = Render<ReferenceItemPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)list.Id));
        panel.Find(".pspad-item-name-field input").Input("Bigos");
        AddDraftField(panel, "Time", "45 min");
        panel.Find(".pspad-markdown-input textarea").Input("Slow cooked");
        panel.Find(".pspad-markdown-input textarea").Blur();
        panel.Render(parameters => parameters.Add(p => p.NewInList, (Guid?)other.Id));

        Assert.Equal("", panel.Find(".pspad-item-name-field input").GetAttribute("value") ?? "");
        Assert.Empty(panel.FindAll(".pspad-field-row"));
        Assert.DoesNotContain("Slow cooked", panel.Find(".pspad-item-description").TextContent);
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

    static void AddDraftField<T>(IRenderedComponent<T> panel, string label, string value) where T : IComponent
    {
        panel.Find(".pspad-field-add-label input").Input(label);
        panel.Find(".pspad-field-add-value input").Input(value);
        panel.Find(".pspad-field-add-value input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    static void OpenRow(IRenderedComponent<ContainerFragment> panel, string row) =>
        panel.Find($"{row} .pspad-property-activator").Click();

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

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

    static ReferenceItem NewItem(Guid listId, string name, DateTimeOffset? createdAt = null)
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), listId, name, 0),
            createdAt ?? DateTimeOffset.UnixEpoch));
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
