using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Pages;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetSharingPageTests : Bunit.TestContext
{
    const string Token = "shared-token-1234567890";
    const string Code = "K7M4PX";
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 9);
    static readonly DateTimeOffset At = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnUnsharedBudgetHasNoMembersButtonButSettingsOffersSharing()
    {
        var budget = NewBudget(Me, "Personal");
        AppTestHost.Arrange(this, Me, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".pspad-budget-sharing")));
        Assert.Empty(page.FindAll(".pspad-budget-members-button"));
        Assert.Contains("Only you", page.Find(".pspad-budget-sharing").TextContent);
        page.Find(".pspad-budget-sharing-open").Click();
        Assert.EndsWith($"?members={budget.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void ASharedBudgetShowsAMembersButtonWithItsHeadCount()
    {
        var budget = Join(Share(NewBudget(Me, "Household")), Other, "Anna");
        AppTestHost.Arrange(this, Me, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".pspad-budget-members-button")));
        Assert.Contains("2", page.Find(".pspad-budget-members-button").TextContent);
        page.Find(".pspad-budget-members-button button").Click();
        Assert.EndsWith($"?members={budget.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void AMemberOpensTheBudgetAndSeesWhoSharedItInsteadOfRenameAndArchive()
    {
        var budget = Join(Share(NewBudget(Other, "Household")), Me, "Lukasz");
        AppTestHost.Arrange(this, Me, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.Contains("Household", page.Markup));
        Assert.Contains("Shared by Kasia.", page.Find(".pspad-budget-shared-by").TextContent);
        Assert.Empty(page.FindAll(".pspad-rename-budget"));
        Assert.Empty(page.FindAll(".pspad-archive-budget"));
        Assert.NotEmpty(page.FindAll(".pspad-category-add"));
    }

    [Fact]
    public void AMemberSeesWhoArchivedTheBudgetAndCannotRestoreIt()
    {
        var budget = Archive(Join(Share(NewBudget(Other, "Trip")), Me, "Lukasz"));
        AppTestHost.Arrange(this, Me, Today, budget);

        var page = Render<BudgetPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Tab, "settings"));

        page.WaitForAssertion(() => Assert.Contains("Kasia archived this budget. It is read-only.", page.Markup));
        Assert.Empty(page.FindAll(".pspad-restore-budget"));
    }

    [Fact]
    public void TheBudgetsPageListsMineFirstThenSharedWithMarkers()
    {
        var mine = Join(Share(NewBudget(Me, "Household")), Other, "Anna");
        var shared = Join(Share(NewBudget(Other, "Trip")), Me, "Lukasz");
        var personal = NewBudget(Me, "Personal", At.AddDays(1));
        AppTestHost.Arrange(this, Me, Today, shared, personal, mine);

        var page = Render<BudgetsPage>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".pspad-budget-card-name").Count));
        Assert.Equal(["Household", "Personal", "Trip"], page.FindAll(".pspad-budget-card-name").Select(name => name.TextContent));
        Assert.Contains("2", page.Find(".pspad-budget-card-shared").TextContent);
        Assert.Contains("Shared by Kasia", page.Find(".pspad-budget-card-shared-by").TextContent);
    }

    static Budget NewBudget(Guid owner, string name, DateTimeOffset? at = null)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), owner, Guid.NewGuid(), name), at ?? At));
        return budget;
    }

    static Budget Share(Budget budget)
    {
        var ownerName = budget.UserId == Other ? "Kasia" : "Lukasz";
        budget.ApplyAll(Budget.Decide(budget, new ShareBudget(Guid.NewGuid(), budget.UserId, budget.Id, Token, Code, ownerName), At));
        return budget;
    }

    static Budget Join(Budget budget, Guid member, string name)
    {
        budget.ApplyAll(Budget.Decide(budget, new JoinBudget(Guid.NewGuid(), member, budget.Id, Token, Code, name), At));
        return budget;
    }

    static Budget Archive(Budget budget)
    {
        budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), budget.UserId, budget.Id), At));
        return budget;
    }
}
