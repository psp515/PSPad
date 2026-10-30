using PSPad.Api.Tests.Sync;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class LiveAudienceTests(MongoFixture fixture)
{
    static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);
    static readonly TimeSpan Briefly = TimeSpan.FromMilliseconds(750);
    static readonly DateOnly Day = new(2026, 10, 9);

    sealed record Lists(Guid Owner, Guid Previous, Guid Current, HttpClient OwnerClient, HttpClient PreviousMember, HttpClient CurrentMember);

    async Task<Lists> TwoSharedListsAsync(ApiFactory factory, ListKind kind, CancellationToken ct)
    {
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var previousToken = Sharing.FreshToken();
        var currentToken = Sharing.FreshToken();
        var previous = await Sharing.SharedListAsync(owner, ownerId, ct, kind, previousToken);
        var current = await Sharing.SharedListAsync(owner, ownerId, ct, kind, currentToken);
        var previousMember = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(previousMember, ct);
        (await Sharing.JoinAsync(previousMember, ct, previousToken)).EnsureSuccessStatusCode();
        var currentMember = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(currentMember, ct);
        (await Sharing.JoinAsync(currentMember, ct, currentToken)).EnsureSuccessStatusCode();
        return new Lists(ownerId, previous, current, owner, previousMember, currentMember);
    }

    [Fact]
    public async Task AMovedTaskNudgesTheMembersOfTheListItLeft()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var lists = await TwoSharedListsAsync(factory, ListKind.Tasks, ct);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(lists.OwnerClient, ct, new CreateTask(Guid.NewGuid(), lists.Owner, taskId, lists.Previous, "Read"));
        await using var leftBehind = await LiveListener.OpenAsync(lists.PreviousMember, ct);
        await using var receiving = await LiveListener.OpenAsync(lists.CurrentMember, ct);

        await Sharing.SendAsync(lists.OwnerClient, ct, new MoveTaskToList(Guid.NewGuid(), lists.Owner, taskId, lists.Current));

        Assert.Equal("sync", await leftBehind.NextAsync(Soon));
        Assert.Equal("sync", await receiving.NextAsync(Soon));
    }

    [Fact]
    public async Task ALaterEditOfAMovedTaskDoesNotReachTheListItLeft()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var lists = await TwoSharedListsAsync(factory, ListKind.Tasks, ct);
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(lists.OwnerClient, ct,
            new CreateTask(Guid.NewGuid(), lists.Owner, taskId, lists.Previous, "Read"),
            new MoveTaskToList(Guid.NewGuid(), lists.Owner, taskId, lists.Current));
        await using var leftBehind = await LiveListener.OpenAsync(lists.PreviousMember, ct);
        await using var receiving = await LiveListener.OpenAsync(lists.CurrentMember, ct);

        await Sharing.SendAsync(lists.OwnerClient, ct, new RenameTask(Guid.NewGuid(), lists.Owner, taskId, "Read more"));

        Assert.Equal("sync", await receiving.NextAsync(Soon));
        Assert.True(await leftBehind.StaysQuietAsync(Briefly));
    }

    [Fact]
    public async Task AMovedReferenceItemNudgesTheMembersOfTheListItLeft()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var lists = await TwoSharedListsAsync(factory, ListKind.Reference, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(lists.OwnerClient, ct, new CreateReferenceItem(Guid.NewGuid(), lists.Owner, itemId, lists.Previous, "Dune", 0));
        await using var leftBehind = await LiveListener.OpenAsync(lists.PreviousMember, ct);
        await using var receiving = await LiveListener.OpenAsync(lists.CurrentMember, ct);

        await Sharing.SendAsync(lists.OwnerClient, ct, new MoveReferenceItemToList(Guid.NewGuid(), lists.Owner, itemId, lists.Current));

        Assert.Equal("sync", await leftBehind.NextAsync(Soon));
        Assert.Equal("sync", await receiving.NextAsync(Soon));
    }

    [Fact]
    public async Task ALaterEditOfAMovedReferenceItemDoesNotReachTheListItLeft()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var lists = await TwoSharedListsAsync(factory, ListKind.Reference, ct);
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(lists.OwnerClient, ct,
            new CreateReferenceItem(Guid.NewGuid(), lists.Owner, itemId, lists.Previous, "Dune", 0),
            new MoveReferenceItemToList(Guid.NewGuid(), lists.Owner, itemId, lists.Current));
        await using var leftBehind = await LiveListener.OpenAsync(lists.PreviousMember, ct);
        await using var receiving = await LiveListener.OpenAsync(lists.CurrentMember, ct);

        await Sharing.SendAsync(lists.OwnerClient, ct, new RenameReferenceItem(Guid.NewGuid(), lists.Owner, itemId, "Dune II"));

        Assert.Equal("sync", await receiving.NextAsync(Soon));
        Assert.True(await leftBehind.StaysQuietAsync(Briefly));
    }

    [Fact]
    public async Task AMembersReferenceItemNudgesTheOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, ListKind.Reference, token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await Sharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(owner, ct);

        await Sharing.SendAsync(member, ct, new CreateReferenceItem(Guid.NewGuid(), memberId, Guid.NewGuid(), listId, "Dune", 0));

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task ARemovedBudgetMemberIsNudged()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(owner, ownerId, ct, token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await BudgetSharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(member, ct);

        (await BudgetSharing.RemoveMemberAsync(owner, budgetId, memberId, ct)).EnsureSuccessStatusCode();

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task AMembersEntryNudgesTheBudgetOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(owner, ownerId, ct, token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await BudgetSharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(owner, ct);

        await Sharing.SendAsync(member, ct, new RecordExpense(
            Guid.NewGuid(), memberId, Guid.NewGuid(), budgetId, "Bread", "Food", new Money(10m, "PLN", 1m, Day), Day, null));

        Assert.Equal("sync", await live.NextAsync(Soon));
    }

    [Fact]
    public async Task AMembersBalanceMonthNudgesTheBudgetOwner()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var token = Sharing.FreshToken();
        var budgetId = await BudgetSharing.SharedBudgetAsync(owner, ownerId, ct, token);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        var memberId = await Sharing.SignInAsync(member, ct);
        (await BudgetSharing.JoinAsync(member, ct, token)).EnsureSuccessStatusCode();
        await using var live = await LiveListener.OpenAsync(owner, ct);

        await Sharing.SendAsync(member, ct, new PSPad.Module.Money.Balance.StartBalanceMonth(
            Guid.NewGuid(), memberId, budgetId, new YearMonth(2026, 10)));

        Assert.Equal("sync", await live.NextAsync(Soon));
    }
}
