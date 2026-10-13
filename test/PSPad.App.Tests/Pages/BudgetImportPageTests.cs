using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.App.State.Outbox;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class BudgetImportPageTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 10, 9);
    const string Entries = "kind,date,name,category,amount,currency,rate,note\n" +
                           "expense,2026-09-03,Bread,Food,4.50,,,\n" +
                           "refund,2026-09-03,Oops,Food,1,,,\n";

    IRenderedComponent<BudgetImportPage> Open(Budget budget)
    {
        var page = Render<BudgetImportPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id));
        page.WaitForAssertion(() => Assert.Contains("Import", page.Markup));
        return page;
    }

    static void Upload(IRenderedComponent<BudgetImportPage> page, int picker, string text) =>
        page.FindComponents<InputFile>()[picker].UploadFiles(InputFileContent.CreateFromText(text, "file.csv"));

    [Fact]
    public void AnEntriesFileShowsReadyAndErrorRows()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, 0, Entries);

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));
        Assert.Contains("Kind must be expense or income.", page.Find(".pspad-import-row-error").TextContent);
        Assert.Contains("Import 1 rows", page.Find(".pspad-import-run").TextContent);
    }

    [Fact]
    public async Task ImportingQueuesTheCommandsAndShowsTheOutcome()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);
        Upload(page, 0, Entries);
        page.WaitForAssertion(() => Assert.False(page.Find(".pspad-import-run").HasAttribute("disabled")));

        page.Find(".pspad-import-run").Click();

        page.WaitForAssertion(() => Assert.Contains("Imported 1 entries", page.Find(".pspad-import-summary").TextContent));
        var queued = await Services.GetRequiredService<IOutbox>().PeekAsync(10);
        Assert.Equal(nameof(PSPad.Module.Money.Entries.RecordExpense), Assert.Single(queued).Envelope.Type);
    }

    [Fact]
    public void AFileErrorShowsUnderItsPicker()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, 1, "month,holding,type\n2026-09,Wallet,cash\n");

        page.WaitForAssertion(() =>
            Assert.Equal("The file has no amount column.", page.Find(".pspad-import-balances .pspad-import-file-error").TextContent.Trim()));
    }

    [Fact]
    public void ImportWaitsForRatesThenUsesThem()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var nbp = (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();
        nbp.Gate = new TaskCompletionSource<NbpRateView?>();
        var page = Open(budget);

        Upload(page, 0, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

        page.WaitForAssertion(() => Assert.True(page.Find(".pspad-import-run").HasAttribute("disabled")));
        nbp.Gate.SetResult(new NbpRateView("EUR", 4.25m, new DateOnly(2026, 9, 4)));
        page.WaitForAssertion(() => Assert.False(page.Find(".pspad-import-run").HasAttribute("disabled")));
        Assert.Contains("4.25", page.Find(".pspad-import-row").TextContent);
    }

    [Fact]
    public void OfflineForeignRowsWithoutARateAreErrors()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        Services.AddSingleton<IConnectivity>(new AppTestHost.Offline());
        var page = Open(budget);

        Upload(page, 0, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

        page.WaitForAssertion(() =>
            Assert.Contains("Needs a rate: go online or fill the rate column.", page.Find(".pspad-import-row-error").TextContent));
        Assert.NotNull(page.Find(".pspad-import-retry"));
    }

    [Fact]
    public void AnArchivedBudgetShowsTheBannerAndNoPickers()
    {
        var budget = BudgetsPageTests.Named("Old", archived: true);
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Assert.NotNull(page.Find(".pspad-archived-banner"));
        Assert.Empty(page.FindComponents<InputFile>());
    }
}
