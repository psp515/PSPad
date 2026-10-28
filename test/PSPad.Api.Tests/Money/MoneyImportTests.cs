using PSPad.Contracts;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Api.Tests.Budgeting;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MoneyImportTests(MongoFixture fixture)
{
    [Fact]
    public async Task RunningThePlanBuildsWhatThePreviewPromised()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var august = new YearMonth(2026, 8);
        var wallet = new Holding(Guid.NewGuid(), "Wallet", HoldingType.Cash, new MoneyValue(10m, "PLN", 1m, august.LastDay));
        var old = new Holding(Guid.NewGuid(), "Old", HoldingType.Savings, new MoneyValue(5m, "PLN", 1m, august.LastDay));
        var create = new CreateBudget(Guid.NewGuid(), user, Guid.NewGuid(), "Personal");
        var start = new StartBalanceMonth(Guid.NewGuid(), user, create.BudgetId, august);
        var addWallet = new AddHolding(Guid.NewGuid(), user, create.BudgetId, august, wallet.HoldingId, wallet.Name, wallet.Type, wallet.Money);
        var addOld = new AddHolding(Guid.NewGuid(), user, create.BudgetId, august, old.HoldingId, old.Name, old.Type, old.Money);
        await SendAsync(client, ct, create, start, addWallet, addOld);
        var now = DateTimeOffset.UtcNow;
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, create, now));
        var augustSnapshot = new BalanceSnapshot();
        augustSnapshot.ApplyAll(BalanceSnapshot.Decide(null, budget, start, now));
        augustSnapshot.ApplyAll(BalanceSnapshot.Decide(augustSnapshot, budget, addWallet, now));
        augustSnapshot.ApplyAll(BalanceSnapshot.Decide(augustSnapshot, budget, addOld, now));
        var preview = MoneyImportPreview.Build(
            MoneyCsv.ParseEntries("kind,date,name,category,amount\nexpense,2026-09-03,Bread,Hobby,4\n"),
            MoneyCsv.ParseBalances("month,holding,type,amount\n2026-09,wallet,cash,20\n2026-09,ETF,investment,7\n" +
                                   "2026-10,ETF,investment,8\n"),
            new ImportContext(budget, [], [augustSnapshot], "PLN"), RateBook.Empty, Guid.NewGuid);

        var plan = MoneyImportPlan.Build(preview, new HashSet<int>(), user, Guid.NewGuid);
        foreach (var batch in plan.Select(planned => (object)planned.Command).Chunk(CommandBatch.MaxEnvelopes))
        {
            await SendAsync(client, ct, batch);
        }

        var snapshots = await SyncedAsync(client, "balancesnapshots", ct);
        foreach (var month in preview.Months)
        {
            var row = Assert.Single(snapshots, row => row.GetProperty("_month").GetString() == month.Month.ToString()
                                                      && !row.GetProperty("deleted").GetBoolean());
            Assert.Equal(month.Holdings, row.GetProperty("_holdings").GetArrayLength());
        }

        var september = Assert.Single(snapshots, row => row.GetProperty("_month").GetString() == "2026-09");
        var septemberHoldings = september.GetProperty("_holdings").EnumerateArray().ToArray();
        Assert.Equal(["wallet", "ETF"], septemberHoldings.Select(holding => holding.GetProperty("name").GetString()));
        Assert.Equal(wallet.HoldingId, septemberHoldings[0].GetProperty("holdingId").GetGuid());
        var october = Assert.Single(snapshots, row => row.GetProperty("_month").GetString() == "2026-10");
        var octoberHolding = Assert.Single(october.GetProperty("_holdings").EnumerateArray().ToArray());
        Assert.Equal("ETF", octoberHolding.GetProperty("name").GetString());
        Assert.Equal(8m, octoberHolding.GetProperty("money").GetProperty("amount").GetDecimal());
        Assert.Contains("Hobby", Assert.Single(await SyncedAsync(client, "budgets", ct))
            .GetProperty("_expenseCategories").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal("Bread", Assert.Single(await SyncedAsync(client, "moneyentries", ct)).GetProperty("name").GetString());
    }
}
