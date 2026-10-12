using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.Sync;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class BudgetMembersPanelTests : Bunit.TestContext
{
    const string Token = "shared-token-1234567890";
    const string Code = "K7M4PX";
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly DateTimeOffset Midnight = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    FakeBudgetSharingApi Api => (FakeBudgetSharingApi)Services.GetRequiredService<IBudgetSharingApi>();

    [Fact]
    public async Task TheOwnerCreatesALink()
    {
        var budget = NewBudget(User);
        var replica = AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);
        Assert.Contains("Only you use this budget", panel.Markup);
        panel.Find(".pspad-invite-create").Click();

        Assert.NotNull((await replica.LoadAsync<Budget>(budget.Id))!.InviteToken);
        Assert.Equal([$"share {budget.Id}"], Api.Calls);
        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
    }

    [Fact]
    public async Task TheOwnerRemovesAMember()
    {
        var budget = Join(Share(NewBudget(User)), Other, "Anna");
        var replica = AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);
        Assert.Contains("Anna", panel.Find(".pspad-share-member").TextContent);
        panel.Find(".pspad-share-remove").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        Assert.Empty((await replica.LoadAsync<Budget>(budget.Id))!.Members);
    }

    [Fact]
    public void OfflineTheOwnerCannotChangeWhoIsIn()
    {
        var budget = Join(Share(NewBudget(User)), Other, "Anna");
        AppTestHost.Arrange(this, User, Today, budget);
        Services.AddSingleton<IConnectivity>(new AppTestHost.Offline());

        var panel = RenderPanel(budget.Id);

        Assert.True(panel.Find(".pspad-share-remove").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-share-stop").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-invite-new").HasAttribute("disabled"));
        Assert.Contains("connection", panel.Find(".pspad-share-offline").TextContent);
    }

    [Fact]
    public void AnArchivedBudgetCannotInvite()
    {
        var budget = Archive(Join(Share(NewBudget(User)), Other, "Anna"));
        AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);

        Assert.Contains("Restore the budget to invite people.", panel.Find(".pspad-invite-blocked").TextContent);
        Assert.True(panel.Find(".pspad-invite-create").HasAttribute("disabled"));
        Assert.False(panel.Find(".pspad-share-remove").HasAttribute("disabled"));
    }

    [Fact]
    public void AMemberSeesWhoSharedItAndNoInvite()
    {
        var budget = Join(Share(NewBudget(Other)), User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);

        Assert.Contains("Kasia", panel.Find(".pspad-share-owner").TextContent);
        Assert.Empty(panel.FindAll(".pspad-invite-card"));
        Assert.Empty(panel.FindAll(".pspad-share-remove"));
        Assert.Contains("Only Kasia can invite people.", panel.Markup);
    }

    [Fact]
    public async Task AMemberLeavesAndGoesBackToBudgets()
    {
        var budget = Join(Share(NewBudget(Other)), User, "Lukasz");
        var replica = AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);
        panel.Find(".pspad-share-leave").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        Assert.Equal([$"leave {budget.Id}"], Api.Calls);
        Assert.Empty((await replica.LoadAsync<Budget>(budget.Id))!.Members);
        Assert.EndsWith("/budgets", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void AStrangerSeesNoPanel()
    {
        var budget = NewBudget(Other);
        AppTestHost.Arrange(this, User, Today, budget);

        var panel = RenderPanel(budget.Id);

        Assert.Empty(panel.FindAll(".pspad-budget-members"));
    }

    IRenderedComponent<ContainerFragment> RenderPanel(Guid budgetId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<BudgetMembersPanel>(2);
        builder.AddAttribute(3, nameof(BudgetMembersPanel.BudgetId), (Guid?)budgetId);
        builder.CloseComponent();
    });

    static Budget NewBudget(Guid owner)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), owner, Guid.NewGuid(), "Household"),
            DateTimeOffset.UnixEpoch));
        return budget;
    }

    static Budget Share(Budget budget)
    {
        var ownerName = budget.UserId == Other ? "Kasia" : "Lukasz";
        budget.ApplyAll(Budget.Decide(budget,
            new ShareBudget(Guid.NewGuid(), budget.UserId, budget.Id, Token, Code, ownerName), Midnight));
        return budget;
    }

    static Budget Join(Budget budget, Guid member, string name)
    {
        budget.ApplyAll(Budget.Decide(budget, new JoinBudget(Guid.NewGuid(), member, budget.Id, Token, Code, name), Midnight));
        return budget;
    }

    static Budget Archive(Budget budget)
    {
        budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), budget.UserId, budget.Id), Midnight));
        return budget;
    }
}
