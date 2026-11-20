using Microsoft.Extensions.Logging.Abstractions;
using PSPad.Module.Reminders.Tests.Fakes;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class ReminderTickTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset LongAgo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Moment = DateTimeOffset.Parse("2026-11-20T08:00:00Z");
    private static readonly DateTimeOffset From = Moment.AddMinutes(-1);

    private readonly FakeReminderClaims claims = new();
    private readonly FakePushSubscriptions subscriptions = new();
    private readonly FakePushSender sender = new();

    private static TodoTask NineOClockTask()
    {
        var task = new TodoTask();
        var id = Guid.NewGuid();
        task.Apply(new TaskCreated(id, Owner, LongAgo, Guid.NewGuid(), "Pay rent"));
        task.Apply(new TaskDueDateSet(id, Owner, LongAgo, new DateOnly(2026, 11, 20)));
        task.Apply(new TaskReminderSet(id, Owner, LongAgo, TaskReminder.Of(0, new TimeOnly(9, 0))));
        return task;
    }

    private ReminderTick TickFor(TodoTask task, params Recipient[] recipients) =>
        new(new FakeReminderSource(new ReminderCandidate(task, recipients)), claims, subscriptions, sender,
            new ReminderSettings("https://pspad.test"), NullLogger<ReminderTick>.Instance);

    private ReminderTick TickFor(params ReminderCandidate[] candidates) =>
        new(new FakeReminderSource(candidates), claims, subscriptions, sender,
            new ReminderSettings("https://pspad.test"), NullLogger<ReminderTick>.Instance);

    private static Recipient Warsaw(Guid user) => new(user, "Europe/Warsaw", "en");

    [Fact]
    public async Task AMomentInTheWindowIsSentToEachDeviceOnce()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Owner, "https://push/b");

        var result = await TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Sent);
        Assert.Equal(2, sender.Endpoints.Count);
        Assert.Equal(ClaimState.Sent, Assert.Single(claims.Claims.Values).State);
    }

    [Fact]
    public async Task TheSameWindowTwiceSendsOnce()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Owner, "https://push/b");
        var tick = TickFor(NineOClockTask(), Warsaw(Owner));

        await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);
        var second = await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(1, second.Skipped);
        Assert.Equal(2, sender.Endpoints.Count);
    }

    [Fact]
    public async Task AUserWithoutDevicesIsSkippedWithoutAClaim()
    {
        var result = await TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Skipped);
        Assert.Empty(claims.Claims);
    }

    [Fact]
    public async Task AGoneDeviceIsForgotten()
    {
        subscriptions.Add(Owner, "https://push/a");
        sender.Queue(410);

        await TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Empty(subscriptions.Records);
    }

    [Fact]
    public async Task AnUnavailableServiceIsRetriedOnlyToDevicesThatMissedIt()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Owner, "https://push/b");
        sender.Queue(201, 503, 503);
        var tick = TickFor(NineOClockTask(), Warsaw(Owner));

        var first = await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);
        var second = await tick.RunAsync(Moment, Moment.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal(0, first.Sent);
        Assert.Equal(1, second.Retried);
        Assert.Equal(["https://push/a", "https://push/b", "https://push/b"], sender.Endpoints);
        var claim = Assert.Single(claims.Claims.Values);
        Assert.Equal(ClaimState.Retry, claim.State);
        Assert.Equal(2, claim.Attempts);
    }

    [Fact]
    public async Task RetriesStopAfterThreeAttempts()
    {
        subscriptions.Add(Owner, "https://push/b");
        sender.Queue(503, 503, 503);
        var tick = TickFor(NineOClockTask(), Warsaw(Owner));

        await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);
        await tick.RunAsync(Moment, Moment.AddMinutes(1), TestContext.Current.CancellationToken);
        var third = await tick.RunAsync(Moment, Moment.AddMinutes(2), TestContext.Current.CancellationToken);
        await tick.RunAsync(Moment, Moment.AddMinutes(3), TestContext.Current.CancellationToken);

        Assert.Equal(1, third.Failed);
        Assert.Equal(3, sender.Endpoints.Count);
        var claim = Assert.Single(claims.Claims.Values);
        Assert.Equal(ClaimState.Failed, claim.State);
        Assert.Equal(3, claim.Attempts);
    }

    [Fact]
    public async Task ARetryThatSucceedsIsSent()
    {
        subscriptions.Add(Owner, "https://push/b");
        sender.Queue(503, 201);
        var tick = TickFor(NineOClockTask(), Warsaw(Owner));

        await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);
        await tick.RunAsync(Moment, Moment.AddMinutes(1), TestContext.Current.CancellationToken);

        var claim = Assert.Single(claims.Claims.Values);
        Assert.Equal(ClaimState.Sent, claim.State);
        Assert.Equal(2, claim.Attempts);
    }

    [Fact]
    public async Task AnExpiredRetryFails()
    {
        subscriptions.Add(Owner, "https://push/a");
        var task = NineOClockTask();
        var now = Moment.AddMinutes(30);
        var claim = new ReminderClaim(
            ReminderClaim.IdFor(task.Id, new DateOnly(2026, 11, 20), Owner), task.Id, new DateOnly(2026, 11, 20),
            Owner, Moment, 1, ClaimState.Retry, new PushNote("t", "b", "u", "g"), now.AddMinutes(-1));
        claims.Claims[claim.Id] = claim;

        var result = await TickFor(task, Warsaw(Owner)).RunAsync(now, now, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Failed);
        Assert.Empty(sender.Endpoints);
        Assert.Equal(ClaimState.Failed, claims.Claims[claim.Id].State);
    }

    [Fact]
    public async Task ARejectedPushFails()
    {
        subscriptions.Add(Owner, "https://push/a");
        sender.Queue(400);

        var result = await TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Failed);
        Assert.Equal(ClaimState.Failed, Assert.Single(claims.Claims.Values).State);
    }

    [Fact]
    public async Task EveryRecipientGetsTheirOwn()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Member, "https://push/m");

        var result = await TickFor(NineOClockTask(), Warsaw(Owner), Warsaw(Member)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Sent);
        Assert.Equal(2, claims.Claims.Count);
    }

    [Fact]
    public async Task AnUnknownTimeZoneFallsBackToUtc()
    {
        subscriptions.Add(Owner, "https://push/a");
        var nineUtc = DateTimeOffset.Parse("2026-11-20T09:00:00Z");

        var result = await TickFor(NineOClockTask(), new Recipient(Owner, "Nowhere/Land", "en"))
            .RunAsync(nineUtc.AddMinutes(-1), nineUtc, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Sent);
    }

    [Fact]
    public async Task OldClaimsArePruned()
    {
        await TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(Moment - TimeSpan.FromDays(7), claims.PrunedBefore);
    }

    [Fact]
    public async Task AThrowingDeviceDoesNotStopTheOthers()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Owner, "https://push/b");
        subscriptions.Add(Member, "https://push/m");
        sender.Throwing.Add("https://push/a");

        var result = await TickFor(
                new ReminderCandidate(NineOClockTask(), [Warsaw(Owner)]),
                new ReminderCandidate(NineOClockTask(), [Warsaw(Member)]))
            .RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(["https://push/a", "https://push/b", "https://push/m"], sender.Endpoints);
        Assert.Equal(2, result.Sent);
        Assert.All(claims.Claims.Values, c => Assert.Equal(ClaimState.Sent, c.State));
    }

    [Fact]
    public async Task AThrowingOnlyDeviceFailsTheClaim()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Member, "https://push/m");
        sender.Throwing.Add("https://push/a");

        var result = await TickFor(NineOClockTask(), Warsaw(Owner), Warsaw(Member))
            .RunAsync(From, Moment, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Sent);
        Assert.Contains(claims.Claims.Values, c => c.UserId == Owner && c.State == ClaimState.Failed);
    }

    [Fact]
    public async Task ACancelledTickStopsInsteadOfFailingTheDevice()
    {
        subscriptions.Add(Owner, "https://push/a");
        sender.Throwing.Add("https://push/a");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        sender.Fault = new OperationCanceledException(cancelled.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TickFor(NineOClockTask(), Warsaw(Owner)).RunAsync(From, Moment, cancelled.Token));
    }

    [Fact]
    public async Task AStoreFaultForOneCandidateLetsTheRestRunThenSurfaces()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Member, "https://push/m");
        subscriptions.Failing.Add(Owner);
        var tick = TickFor(
            new ReminderCandidate(NineOClockTask(), [Warsaw(Owner)]),
            new ReminderCandidate(NineOClockTask(), [Warsaw(Member)]));

        await Assert.ThrowsAsync<AggregateException>(
            () => tick.RunAsync(From, Moment, TestContext.Current.CancellationToken));

        Assert.Equal(["https://push/m"], sender.Endpoints);
        Assert.Equal(Moment - TimeSpan.FromDays(7), claims.PrunedBefore);
    }

    [Fact]
    public async Task AStoreFaultInARetryLetsTheOtherRetriesRun()
    {
        subscriptions.Add(Owner, "https://push/a");
        subscriptions.Add(Member, "https://push/m");
        sender.Queue(503, 503);
        var tick = TickFor(NineOClockTask(), Warsaw(Owner), Warsaw(Member));
        await tick.RunAsync(From, Moment, TestContext.Current.CancellationToken);
        subscriptions.Failing.Add(Owner);

        await Assert.ThrowsAsync<AggregateException>(
            () => tick.RunAsync(Moment, Moment.AddMinutes(1), TestContext.Current.CancellationToken));

        Assert.Equal(["https://push/a", "https://push/m", "https://push/m"], sender.Endpoints);
        Assert.Equal(ClaimState.Sent, claims.Claims.Values.Single(c => c.UserId == Member).State);
    }
}
