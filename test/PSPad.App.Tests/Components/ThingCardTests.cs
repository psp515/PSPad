using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ThingCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void ItPreviewsAtMostFiveOpenTasks()
    {
        Arrange();

        var card = Render(Tasks(7));

        Assert.Equal(5, card.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void ShowAllLeadsToTheHrefAndCountsEveryOpenTask()
    {
        Arrange();

        var card = Render(Tasks(7));
        var showAll = card.Find(".pspad-show-all");

        Assert.Equal("Show all 7", showAll.TextContent.Trim());
        Assert.Equal("/things/1", showAll.GetAttribute("href"));
    }

    [Fact]
    public void FiveTasksNeedNoShowAll()
    {
        Arrange();

        var card = Render(Tasks(5));

        Assert.Empty(card.FindAll(".pspad-show-all"));
    }

    [Fact]
    public void TheTitleLinksToTheHref()
    {
        Arrange();

        var card = Render(Tasks(1));

        Assert.Equal("/things/1", card.Find("a.pspad-thing-name").GetAttribute("href"));
        Assert.Contains("Thing", card.Find("a.pspad-thing-name").TextContent);
    }

    [Fact]
    public void TheCountIsAPillWithTheNumberAndAnAccessibleLabel()
    {
        Arrange();

        var count = Render(Tasks(7)).Find(".pspad-open-count");

        Assert.Equal("7 open", count.TextContent);
        Assert.Equal(" open", count.QuerySelector(".pspad-sr-only")!.TextContent);
        Assert.Null(count.GetAttribute("aria-label"));
    }

    [Fact]
    public void TheIconTileIsTheCollapseToggleAndThereIsNoChevron()
    {
        var collapse = Arrange();

        var card = Render(Tasks(3));
        var tile = card.Find("button.pspad-card-tile");

        Assert.Single(card.FindAll("button"), button => button.ClassList.Contains("pspad-card-tile"));
        Assert.Equal("true", tile.GetAttribute("aria-expanded"));
        Assert.Equal("Collapse Thing", tile.GetAttribute("aria-label"));
        tile.Click();
        Assert.True(collapse.IsCollapsed(Id));
        tile = card.Find("button.pspad-card-tile");
        Assert.Equal("false", tile.GetAttribute("aria-expanded"));
        Assert.Equal("Expand Thing", tile.GetAttribute("aria-label"));
        Assert.False(card.FindComponent<MudCollapse>().Instance.Expanded);
    }

    [Fact]
    public void WithoutAnIconTheTileShowsTheDefaultListIcon()
    {
        Arrange();

        var tile = Render(Tasks(1)).Find("button.pspad-card-tile");

        Assert.Contains(IconPaths.DistinctivePath(Icons.Material.Outlined.List), tile.InnerHtml);
    }

    [Fact]
    public void TheCardHasNoDividers()
    {
        Arrange();

        Assert.Empty(Render(Tasks(7)).FindComponents<MudDivider>());
    }

    [Fact]
    public void TheEmptyTextShowsOnlyWithNoOpenTasks()
    {
        Arrange();

        var empty = Render<ThingCard>(parameters => Base(parameters, []).Add(p => p.EmptyText, "No open tasks."));
        var busy = Render<ThingCard>(parameters => Base(parameters, Tasks(1)).Add(p => p.EmptyText, "No open tasks."));

        Assert.Contains("No open tasks.", empty.Markup);
        Assert.DoesNotContain("No open tasks.", busy.Markup);
    }

    [Fact]
    public void RowCaptionsComeFromTheCallersFunctions()
    {
        Arrange();

        var card = Render<ThingCard>(parameters => Base(parameters, Tasks(1))
            .Add(p => p.ListNameOf, _ => "Groceries"));
        var row = card.FindComponent<TaskRow>().Instance;

        Assert.Equal("Groceries", row.ListName);
    }

    [Fact]
    public void SubtitleAndActionsRenderInTheHeader()
    {
        Arrange();

        var card = Render<ThingCard>(parameters => Base(parameters, Tasks(1))
            .Add(p => p.Subtitle, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"sub\">Due soon</span>")))
            .Add(p => p.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span class=\"act\">menu</span>"))));

        Assert.Same(card.Find("a.pspad-thing-name").ParentElement, card.Find(".sub").ParentElement);
        card.Find(".act");
    }

    [Fact]
    public void ACollapsedCardFoldsItsBodyAway()
    {
        var collapse = Arrange();
        Toggle(collapse, Id);

        var card = Render(Tasks(3));

        Assert.False(card.FindComponent<MudCollapse>().Instance.Expanded);
    }

    [Fact]
    public void AnOpenCardUnfoldsItsBody()
    {
        Arrange();

        var card = Render(Tasks(3));

        Assert.True(card.FindComponent<MudCollapse>().Instance.Expanded);
        Assert.Equal(3, card.FindComponents<TaskRow>().Count);
    }

    [Fact]
    public void RowsOverridesTaskRowsWhenSet()
    {
        Arrange();

        var card = Render<ThingCard>(parameters => Base(parameters, Tasks(3))
            .Add(p => p.Rows, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div class=\"pspad-custom-row\">Row</div>")))
            .Add(p => p.RowCount, 9));

        Assert.Empty(card.FindComponents<TaskRow>());
        card.Find(".pspad-custom-row");
        Assert.Equal("9 open", card.Find(".pspad-open-count").TextContent);
        Assert.Equal("Show all 9", card.Find(".pspad-show-all").TextContent.Trim());
    }

    [Fact]
    public void EmptyTextShowsInsteadOfRowsWhenRowCountIsZero()
    {
        Arrange();

        var card = Render<ThingCard>(parameters => Base(parameters, [])
            .Add(p => p.Rows, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div class=\"pspad-custom-row\">Row</div>")))
            .Add(p => p.RowCount, 0)
            .Add(p => p.EmptyText, "No items yet."));

        Assert.Contains("No items yet.", card.Markup);
        Assert.Empty(card.FindAll(".pspad-custom-row"));
    }

    [Fact]
    public void CompletedAndDeletedTasksAreNotPreviewed()
    {
        Arrange();
        var tasks = Tasks(3);
        tasks[0].ApplyAll(TodoTask.Decide(tasks[0], new CompleteTask(Guid.NewGuid(), User, tasks[0].Id), DateTimeOffset.UnixEpoch));
        tasks[1].ApplyAll(TodoTask.Decide(tasks[1], new DeleteTask(Guid.NewGuid(), User, tasks[1].Id), DateTimeOffset.UnixEpoch));

        var card = Render(tasks);

        Assert.Single(card.FindComponents<TaskRow>());
        Assert.Equal("1 open", card.Find(".pspad-open-count").TextContent);
    }

    IRenderedComponent<ThingCard> Render(IReadOnlyList<TodoTask> tasks) =>
        Render<ThingCard>(parameters => Base(parameters, tasks));

    static ComponentParameterCollectionBuilder<ThingCard> Base(
        ComponentParameterCollectionBuilder<ThingCard> parameters, IReadOnlyList<TodoTask> tasks) =>
        parameters
            .Add(p => p.Id, Id)
            .Add(p => p.Name, "Thing")
            .Add(p => p.Href, "/things/1")
            .Add(p => p.TitleClass, "pspad-thing-name")
            .Add(p => p.Tasks, tasks)
            .Add(p => p.Today, Today);

    CardCollapseState Arrange()
    {
        AppTestHost.Arrange(this, User, Today);
        return Services.GetRequiredService<CardCollapseState>();
    }

    static void Toggle(CardCollapseState collapse, Guid id) =>
        collapse.ToggleAsync(id).GetAwaiter().GetResult();

    static TodoTask[] Tasks(int count) =>
        [.. Enumerable.Range(0, count).Select(index =>
        {
            var task = new TodoTask();
            task.ApplyAll(TodoTask.Decide(
                null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), $"Task {index}"),
                DateTimeOffset.UnixEpoch));
            return task;
        })];
}
