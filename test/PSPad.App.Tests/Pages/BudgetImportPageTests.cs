using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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

    [Fact]
    public void OverlappingRateFetchesResolvingInReverseOrderLeaveNoRowWaiting()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var nbp = (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();
        nbp.GatesByCurrency["EUR"] = new TaskCompletionSource<NbpRateView?>();
        nbp.GatesByCurrency["USD"] = new TaskCompletionSource<NbpRateView?>();
        var page = Open(budget);

        Upload(page, 1, "month,holding,type,amount,currency\n2026-09,Wallet,cash,10,USD\n");
        Upload(page, 0, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");
        nbp.GatesByCurrency["EUR"].SetResult(new NbpRateView("EUR", 4.25m, new DateOnly(2026, 9, 4)));
        nbp.GatesByCurrency["USD"].SetResult(new NbpRateView("USD", 3.9m, new DateOnly(2026, 9, 29)));

        page.WaitForAssertion(() => Assert.False(page.Find(".pspad-import-run").HasAttribute("disabled")));
        Assert.DoesNotContain("Fetching rate", page.Markup);
    }

    [Fact]
    public void AThrowingRateServiceMarksTheRowsFailedAndKeepsRetryAvailable()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        Services.AddSingleton<INbpRates>(new ThrowingRates());
        var page = Open(budget);

        Upload(page, 0, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

        page.WaitForAssertion(() =>
            Assert.Contains("Couldn't fetch the NBP rate.", page.Find(".pspad-import-row-error").TextContent));
        Assert.False(page.Find(".pspad-import-retry").HasAttribute("disabled"));
    }

    [Fact]
    public void RateSourceIsShownAndTheNbpChipCountsRowsNotRequests()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var nbp = (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();
        nbp.Next = new NbpRateView("EUR", 4.25m, new DateOnly(2026, 9, 4));
        var page = Open(budget);

        Upload(page, 0, "kind,date,name,category,amount,currency,rate\n" +
                        "expense,2026-09-05,Steam,PC,19.99,EUR,\n" +
                        "expense,2026-09-06,Game,PC,5,EUR,\n" +
                        "expense,2026-09-07,Own,PC,5,EUR,4.5\n");

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".pspad-import-row").Count));
        page.WaitForAssertion(() => Assert.Contains("4.25 (NBP 2026-09-04)", page.Markup));
        Assert.Contains("4.5 (file)", page.Markup);
        Assert.Contains("Rates from NBP 2", page.Find(".pspad-import-chips").TextContent);
    }

    [Fact]
    public void BalancesAreGroupedUnderTheirMonthAndFilterable()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, 1, "month,holding,type,amount\n2026-08,Wallet,cash,10\n2026-09,Wallet,cash,20\n");

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-month").Count));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("table").Count));
        Assert.Contains("Errors", page.Markup);
    }

    [Fact]
    public void LeavingBeforeAnyFileIsPickedAsksNothing()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var host = RenderWithDialogs(budget);
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/budgets");

        host.WaitForAssertion(() => Assert.EndsWith("/budgets", navigation.Uri));
        Assert.Empty(host.FindAll("div.mud-dialog"));
    }

    [Fact]
    public void LeavingWithPickedDataAsksAndCancelStays()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var host = RenderWithDialogs(budget);
        var page = host.FindComponent<BudgetImportPage>();
        page.WaitForAssertion(() => Assert.Contains("Import", page.Markup));
        Upload(page, 0, Entries);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;

        navigation.NavigateTo("/budgets");

        host.WaitForAssertion(() => Assert.Equal("Leave the import? The files you picked won’t be imported.",
            host.Find("div.mud-dialog .mud-dialog-content").TextContent.Trim()));
        host.FindAll("div.mud-dialog button").First().Click();
        host.WaitForAssertion(() => Assert.Empty(host.FindAll("div.mud-dialog")));
        Assert.Equal(before, navigation.Uri);
    }

    [Fact]
    public async Task LeavingWithPickedDataAndConfirmingLeaves()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var host = RenderWithDialogs(budget);
        var page = host.FindComponent<BudgetImportPage>();
        page.WaitForAssertion(() => Assert.Contains("Import", page.Markup));
        Upload(page, 0, Entries);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/budgets");
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("div.mud-dialog")));
        host.FindAll("div.mud-dialog button").Last().Click();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!navigation.Uri.EndsWith("/budgets") && DateTime.UtcNow < deadline)
            await Task.Delay(20, Xunit.TestContext.Current.CancellationToken);
        Assert.EndsWith("/budgets", navigation.Uri);
    }

    [Fact]
    public void LeavingAfterTheImportFinishedAsksNothing()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var host = RenderWithDialogs(budget);
        var page = host.FindComponent<BudgetImportPage>();
        page.WaitForAssertion(() => Assert.Contains("Import", page.Markup));
        Upload(page, 0, Entries);
        page.WaitForAssertion(() => Assert.False(page.Find(".pspad-import-run").HasAttribute("disabled")));
        page.Find(".pspad-import-run").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find(".pspad-import-summary")));
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/budgets");

        host.WaitForAssertion(() => Assert.EndsWith("/budgets", navigation.Uri));
        Assert.Empty(host.FindAll("div.mud-dialog"));
    }

    IRenderedComponent<ContainerFragment> RenderWithDialogs(Budget budget) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<BudgetImportPage>(2);
        builder.AddAttribute(3, nameof(BudgetImportPage.BudgetId), budget.Id);
        builder.CloseComponent();
    });

    sealed class ThrowingRates : INbpRates
    {
        public Task<NbpRateView?> RateAsync(string currency, DateOnly date) => throw new InvalidOperationException("boom");
    }
}
