using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Pages;
using PSPad.App.State;
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

    IRenderedComponent<BudgetImportPage> Open(Budget budget, string kind = BudgetLinks.EntriesImport)
    {
        var page = Render<BudgetImportPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Kind, kind));
        page.WaitForAssertion(() => Assert.Contains("Import", page.Markup));
        return page;
    }

    static void Upload(IRenderedComponent<BudgetImportPage> page, string text) =>
        Assert.Single(page.FindComponents<InputFile>()).UploadFiles(InputFileContent.CreateFromText(text, "file.csv"));

    [Fact]
    public void AnEntriesFileShowsReadyAndErrorRows()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, Entries);

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
        Upload(page, Entries);
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
        var page = Open(budget, BudgetLinks.BalancesImport);

        Upload(page, "month,holding,type\n2026-09,Wallet,cash\n");

        page.WaitForAssertion(() =>
            Assert.Equal("The file has no amount column.", page.Find(".pspad-import-file-error").TextContent.Trim()));
    }

    [Fact]
    public void ImportWaitsForRatesThenUsesThem()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var nbp = (AppTestHost.FakeNbpRates)Services.GetRequiredService<INbpRates>();
        nbp.Gate = new TaskCompletionSource<NbpRateView?>();
        var page = Open(budget);

        Upload(page, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

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

        Upload(page, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

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

        Upload(page, "kind,date,name,category,amount,currency\n" +
                     "expense,2026-09-05,Steam,PC,19.99,EUR\n" +
                     "expense,2026-09-29,Amazon,PC,10,USD\n");
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

        Upload(page, "kind,date,name,category,amount,currency\nexpense,2026-09-05,Steam,PC,19.99,EUR\n");

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

        Upload(page, "kind,date,name,category,amount,currency,rate\n" +
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
        var page = Open(budget, BudgetLinks.BalancesImport);

        Upload(page, "month,holding,type,amount\n2026-08,Wallet,cash,10\n2026-09,Wallet,cash,20\n");

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-month").Count));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("table").Count));
        Assert.Contains("Errors", page.Markup);
    }

    [Fact]
    public void TheEntriesPageWaitsOnADropZoneAndLinksItsTemplate()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Assert.Contains("Import entries into Personal", page.Find("h1").TextContent);
        Assert.Contains("templates/pspad-entries.csv", page.Find(".pspad-import-template").GetAttribute("href"));
        Assert.Empty(page.FindAll(".pspad-import-row"));
        page.Find(".pspad-import-drop button.mud-file-upload-dragarea").Click();

        JSInterop.VerifyInvoke("mudFileUpload.openFilePicker");
    }

    [Fact]
    public void TheBalancesPageShowsOnlyBalances()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget, BudgetLinks.BalancesImport);

        Upload(page, "month,holding,type,amount\n2026-09,Wallet,cash,20\n");

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".pspad-import-month")));
        Assert.Contains("Import balances into Personal", page.Find("h1").TextContent);
        Assert.Contains("templates/pspad-balances.csv", page.Find(".pspad-import-template").GetAttribute("href"));
        Assert.Empty(page.FindAll(".mud-tabs"));
        Assert.DoesNotContain("New categories", page.Markup);
    }

    [Fact]
    public void AnEntriesFileShowsNoTabs()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, Entries);

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));
        Assert.Empty(page.FindAll(".mud-tabs"));
    }

    [Fact]
    public void TheErrorsFilterShowsOnlyErrorRowsInTheErrorColour()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);
        Upload(page, Entries);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));

        page.FindAll(".pspad-import-filter .mud-chip").Single(chip => chip.TextContent.Trim() == "Errors").Click();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".pspad-import-row")));
        Assert.Contains("Kind must be expense or income.", page.Find(".pspad-import-row-error .mud-error-text").TextContent);
        Assert.Equal("true", page.FindAll(".pspad-import-filter .mud-chip")
            .Single(chip => chip.TextContent.Trim() == "Errors").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void NewCategoriesShowAsChipsPerKind()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Open(budget);

        Upload(page, "kind,date,name,category,amount\n" +
                     "expense,2026-09-03,Lego,Hobby,10\n" +
                     "expense,2026-09-04,Steam,Gry,20\n" +
                     "income,2026-09-05,Dividend,Przychody,5\n");

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".pspad-import-row").Count));
        Assert.Equal(["Hobby", "Gry"],
            page.FindAll(".pspad-import-new-expense .mud-chip").Select(chip => chip.TextContent.Trim()));
        Assert.Equal(["Przychody"],
            page.FindAll(".pspad-import-new-income .mud-chip").Select(chip => chip.TextContent.Trim()));
    }

    [Fact]
    public void AnUnknownKindIsNotFound()
    {
        var budget = BudgetsPageTests.Named("Personal");
        AppTestHost.Arrange(this, budget.UserId, Today, budget);
        var page = Render<BudgetImportPage>(parameters => parameters.Add(p => p.BudgetId, budget.Id).Add(p => p.Kind, "receipts"));

        page.WaitForAssertion(() => Assert.Contains("That import does not exist.", page.Markup));
        Assert.Empty(page.FindComponents<InputFile>());
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
        Upload(page, Entries);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".pspad-import-row").Count));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;

        navigation.NavigateTo("/budgets");

        host.WaitForAssertion(() => Assert.Equal("Leave the import? The file you picked won’t be imported.",
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
        Upload(page, Entries);
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
        Upload(page, Entries);
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
        builder.AddAttribute(4, nameof(BudgetImportPage.Kind), BudgetLinks.EntriesImport);
        builder.CloseComponent();
    });

    sealed class ThrowingRates : INbpRates
    {
        public Task<NbpRateView?> RateAsync(string currency, DateOnly date) => throw new InvalidOperationException("boom");
    }
}
