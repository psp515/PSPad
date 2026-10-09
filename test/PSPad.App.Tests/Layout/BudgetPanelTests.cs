using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
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
}
