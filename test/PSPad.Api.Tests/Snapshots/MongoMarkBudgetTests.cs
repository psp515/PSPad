using PSPad.Api.Snapshots;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Snapshots;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoMarkBudgetTests(MongoFixture fixture)
{
    static readonly DateOnly Day = new(2026, 10, 12);

    [Fact]
    public async Task ItGrantsUpToTheLimitThenRefuses()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var budget = new MongoMarkBudget(Persistence.TestContext.For(fixture));
        var listId = Guid.NewGuid();

        var granted = new List<bool>();
        for (var take = 0; take < 4; take++)
        {
            granted.Add(await budget.TryTakeAsync(listId, Day, 3, ct));
        }

        Assert.Equal([true, true, true, false], granted);
    }

    [Fact]
    public async Task EachListAndDayHasItsOwnBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var budget = new MongoMarkBudget(Persistence.TestContext.For(fixture));
        var listId = Guid.NewGuid();
        Assert.True(await budget.TryTakeAsync(listId, Day, 1, ct));

        Assert.True(await budget.TryTakeAsync(listId, Day.AddDays(1), 1, ct));
        Assert.True(await budget.TryTakeAsync(Guid.NewGuid(), Day, 1, ct));
    }

    [Fact]
    public async Task ConcurrentTakesNeverExceedTheLimit()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var budget = new MongoMarkBudget(Persistence.TestContext.For(fixture));
        var listId = Guid.NewGuid();

        var granted = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => budget.TryTakeAsync(listId, Day, 5, ct)));

        Assert.Equal(5, granted.Count(taken => taken));
    }
}
