using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ListToolbarTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void TheFilterButtonCountsActiveChoices()
    {
        var choice = ListScreenChoice.Default with
        {
            Filter = ListFilter.None with { When = new HashSet<WhenBucket> { WhenBucket.Today }, Starred = true }
        };

        var bar = RenderBar(choice);

        Assert.Equal("2", bar.Find(".pspad-list-filter-badge").TextContent.Trim());
    }

    [Fact]
    public void NoBadgeWithoutAFilter() =>
        Assert.Empty(RenderBar(ListScreenChoice.Default).FindAll(".pspad-list-filter-badge"));

    [Fact]
    public void PickingAChipRaisesTheNewChoice()
    {
        ListScreenChoice? raised = null;
        var bar = RenderBar(ListScreenChoice.Default, changed => raised = changed);

        bar.Find(".pspad-list-filter").Click();
        bar.FindAll(".pspad-list-filter-chip").First(chip => chip.TextContent.Contains("Overdue")).Click();

        Assert.Equal([WhenBucket.Overdue], raised!.Filter.When);
    }

    [Fact]
    public void PickingAChosenChipAgainDropsIt()
    {
        ListScreenChoice? raised = null;
        var choice = ListScreenChoice.Default with
        {
            Filter = ListFilter.None with { Priorities = new HashSet<Priority> { Priority.High, Priority.Low } }
        };
        var bar = RenderBar(choice, changed => raised = changed);

        bar.Find(".pspad-list-filter").Click();
        bar.FindAll(".pspad-list-filter-chip").First(chip => chip.TextContent.Contains("High")).Click();

        Assert.Equal([Priority.Low], raised!.Filter.Priorities);
    }

    [Fact]
    public void EachActiveGroupIsOneClosableChip()
    {
        var choice = ListScreenChoice.Default with
        {
            Filter = ListFilter.None with
            {
                When = new HashSet<WhenBucket> { WhenBucket.Today, WhenBucket.Overdue },
                Priorities = new HashSet<Priority> { Priority.High }
            }
        };

        var chips = RenderBar(choice).FindAll(".pspad-list-active-chips .mud-chip");

        Assert.Equal(["Overdue, Today", "High"], chips.Select(chip => chip.TextContent.Trim()));
    }

    [Fact]
    public void ClearResetsTheFilterButKeepsTheSort()
    {
        ListScreenChoice? raised = null;
        var choice = new ListScreenChoice(
            ListFilter.None with { Starred = true }, new ListSort(ListOrder.Name, false), ListScreenView.List);
        var bar = RenderBar(choice, changed => raised = changed);

        bar.Find(".pspad-list-active-chips .pspad-list-filter-clear").Click();

        Assert.True(raised!.Filter.IsEmpty);
        Assert.Equal(ListOrder.Name, raised.Sort.Order);
    }

    [Theory]
    [InlineData(ListOrder.Date, false, "Earliest first")]
    [InlineData(ListOrder.Date, true, "Latest first")]
    [InlineData(ListOrder.Name, false, "A → Z")]
    [InlineData(ListOrder.Added, true, "Oldest first")]
    public void TheSortMenuNamesTheDirectionForItsOrder(ListOrder order, bool descending, string label)
    {
        var bar = RenderBar(ListScreenChoice.Default with { Sort = new ListSort(order, descending) });

        bar.Find(".pspad-list-sort").Click();

        Assert.Contains(label, bar.Markup);
    }

    [Fact]
    public void PickingAnOrderRaisesItAscending()
    {
        ListScreenChoice? raised = null;
        var bar = RenderBar(ListScreenChoice.Default with { Sort = new ListSort(ListOrder.Date, true) }, changed => raised = changed);

        bar.Find(".pspad-list-sort").Click();
        bar.FindAll(".pspad-list-sort-option").First(option => option.TextContent.Contains("Name")).Click();

        Assert.Equal(new ListSort(ListOrder.Name, false), raised!.Sort);
    }

    [Fact]
    public void GoalChipsNameTheOfferedGoals()
    {
        ListScreenChoice? raised = null;
        var goal = NewGoal("Move to Kraków");
        var bar = RenderBar(ListScreenChoice.Default, changed => raised = changed, [goal]);

        bar.Find(".pspad-list-filter").Click();
        bar.FindAll(".pspad-list-filter-chip").First(chip => chip.TextContent.Contains("Move to Kraków")).Click();

        Assert.Equal([goal.Id], raised!.Filter.GoalIds);
    }

    [Fact]
    public void TheViewToggleRaisesCalendar()
    {
        ListScreenChoice? raised = null;
        var bar = RenderBar(ListScreenChoice.Default, changed => raised = changed);

        bar.FindAll(".pspad-list-views .mud-toggle-item").Last().Click();

        Assert.Equal(ListScreenView.Calendar, raised!.View);
    }

    IRenderedComponent<ContainerFragment> RenderBar(
        ListScreenChoice choice, Action<ListScreenChoice>? onChanged = null, IReadOnlyList<Goal>? goals = null)
    {
        AppTestHost.Arrange(this, User, Today);
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ListToolbar>(1);
            builder.AddAttribute(2, nameof(ListToolbar.Choice), choice);
            builder.AddAttribute(3, nameof(ListToolbar.ChoiceChanged),
                EventCallback.Factory.Create<ListScreenChoice>(this, changed => onChanged?.Invoke(changed)));
            builder.AddAttribute(4, nameof(ListToolbar.Goals), goals ?? []);
            builder.AddAttribute(5, nameof(ListToolbar.Shown), 3);
            builder.AddAttribute(6, nameof(ListToolbar.Total), 8);
            builder.CloseComponent();
        };
        return Render(fragment);
    }

    static Goal NewGoal(string name)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(
            null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        return goal;
    }
}
