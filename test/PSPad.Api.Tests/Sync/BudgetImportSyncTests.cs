using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class BudgetImportSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnImportPlanLandsInBatchesAndANewMonthHoldsExactlyItsRows()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await Sharing.SignInAsync(client, ct);
        var august = new YearMonth(2026, 8);
        var create = new CreateBudget(Guid.NewGuid(), user, Guid.NewGuid(), "Personal");
        var start = new StartBalanceMonth(Guid.NewGuid(), user, create.BudgetId, august);
        var add = new AddHolding(Guid.NewGuid(), user, create.BudgetId, august, Guid.NewGuid(), "Old", HoldingType.Cash,
            new Money(1m, "PLN", 1m, august.LastDay));
        await Sharing.SendAsync(client, ct, create, start, add);
        var now = DateTimeOffset.UtcNow;
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, create, now));
        var augustSnapshot = new BalanceSnapshot();
        augustSnapshot.ApplyAll(BalanceSnapshot.Decide(null, budget, start, now));
        augustSnapshot.ApplyAll(BalanceSnapshot.Decide(augustSnapshot, budget, add, now));
        var entries = "kind,date,name,category,amount\n" +
                      string.Concat(Enumerable.Range(1, 118).Select(day => $"expense,2026-09-{day % 28 + 1:D2},Item {day},Food,{day}\n"));
        var preview = MoneyImportPreview.Build(MoneyCsv.ParseEntries(entries),
            MoneyCsv.ParseBalances("month,holding,type,amount\n2026-09,Wallet,cash,10\n"),
            new ImportContext(budget, [], [augustSnapshot], "PLN"), RateBook.Empty, Guid.NewGuid);
        var plan = MoneyImportPlan.Build(preview, new HashSet<int>(), user, Guid.NewGuid);

        foreach (var batch in plan.Select(planned => (object)planned.Command).Chunk(CommandBatch.MaxEnvelopes))
        {
            await Sharing.SendAsync(client, ct, batch);
        }

        var entryRows = new List<JsonElement>();
        var snapshotRows = new List<JsonElement>();
        var marker = 0L;
        while (true)
        {
            var page = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={marker}", ct);
            entryRows.AddRange(page!.Documents["moneyentries"]);
            snapshotRows.AddRange(page.Documents["balancesnapshots"]);
            marker = page.Marker;
            if (!page.HasMore)
            {
                break;
            }
        }

        Assert.Equal(118, entryRows.Count);
        var september = Assert.Single(snapshotRows, row => row.GetProperty("_month").GetString() == "2026-09");
        var holding = Assert.Single(september.GetProperty("_holdings").EnumerateArray().ToArray());
        Assert.Equal("Wallet", holding.GetProperty("name").GetString());
    }
}
