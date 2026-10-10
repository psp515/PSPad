using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.App.Tests.Pages;
using PSPad.App.State.Outbox;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class BudgetPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public async Task CreatingSendsCreateBudgetWithTheTypedName()
    {
        AppTestHost.Arrange(this, User, Today);
        var outbox = Services.GetRequiredService<IOutbox>();

        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-budget-name-field input").Input("Household");
        panel.Find(".pspad-panel-save").Click();

        var entries = await outbox.PeekAsync(10);
        var entry = Assert.Single(entries);
        Assert.Equal(nameof(CreateBudget), entry.Envelope.Type);
        Assert.Equal("Household", entry.Envelope.Payload.Deserialize<CreateBudget>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Name);
    }

    [Fact]
    public void SaveIsDisabledForABlankName()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.IsNew, true));
        Assert.Contains("Add budget", panel.Find(".pspad-panel-save").TextContent);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));

        panel.Find(".pspad-budget-name-field input").Input("   ");

        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ARejectedCreateStaysOnThePanelAndShowsTheRejection()
    {
        AppTestHost.Arrange(this, User, Today);
        var startUri = Services.GetRequiredService<NavigationManager>().Uri;
        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.IsNew, true));

        panel.Find(".pspad-budget-name-field input").Input(new string('x', 81));
        panel.Find(".pspad-panel-save").Click();

        Assert.Equal(startUri, Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message?.Contains("A budget name is at most 80 characters.") == true);
        Assert.Equal(new string('x', 81), panel.Find(".pspad-budget-name-field input").GetAttribute("value"));
    }

    [Fact]
    public void TheNameFieldCapsAtEightyCharacters()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.IsNew, true));

        Assert.Equal("80", panel.Find(".pspad-budget-name-field input").GetAttribute("maxlength"));
    }

    [Fact]
    public async Task RenamingAnExistingBudgetSendsRenameBudget()
    {
        var budget = BudgetsPageTests.Named("Old");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        panel.WaitForAssertion(() => Assert.Equal("Old", panel.Find(".pspad-budget-name-field input").GetAttribute("value")));

        panel.Find(".pspad-budget-name-field input").Change("New");

        var entry = Assert.Single(await Services.GetRequiredService<IOutbox>().PeekAsync(10));
        Assert.Equal(nameof(RenameBudget), entry.Envelope.Type);
        Assert.Equal("New", entry.Envelope.Payload.Deserialize<RenameBudget>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Name);
    }

    [Fact]
    public void ARejectedRenameShowsTheRejection()
    {
        var budget = BudgetsPageTests.Named("Old");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var panel = Render<BudgetPanel>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        panel.WaitForAssertion(() => Assert.Equal("Old", panel.Find(".pspad-budget-name-field input").GetAttribute("value")));

        panel.Find(".pspad-budget-name-field input").Change(new string('y', 81));

        Assert.Contains(Services.GetRequiredService<ISnackbar>().ShownSnackbars,
            snackbar => snackbar.Message?.Contains("A budget name is at most 80 characters.") == true);
    }
}
