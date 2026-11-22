using Microsoft.Extensions.Logging;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Module.Reminders;

public sealed class ReminderTick(
    IReminderSource source,
    IReminderClaims claims,
    IPushSubscriptionStore subscriptions,
    IPushSender sender,
    ReminderSettings settings,
    ILogger<ReminderTick> logger)
{
    public static readonly TimeSpan CatchUp = TimeSpan.FromHours(1);
    public const int MostAttempts = 3;
    public static readonly TimeSpan KeepClaims = TimeSpan.FromDays(7);

    public async Task<TickResult> RunAsync(DateTimeOffset from, DateTimeOffset now, CancellationToken ct)
    {
        var tally = new Tally();
        var faults = new List<Exception>();

        foreach (var candidate in await source.CandidatesAsync(ct))
        {
            var task = candidate.Task;
            if (task.Reminder is null)
            {
                continue;
            }

            foreach (var recipient in candidate.Recipients)
            {
                var zone = ZoneOf(recipient.TimeZone);

                foreach (var moment in ReminderRule.MomentsBetween(task, zone, from, now))
                {
                    await IsolatedAsync(() => RemindAsync(task, recipient, zone, moment, now, tally, ct), faults, ct);
                }
            }
        }

        foreach (var claim in await claims.RetriesSinceAsync(now - CatchUp, ct))
        {
            await IsolatedAsync(() => RetryAsync(claim, now, tally, ct), faults, ct);
        }

        await claims.PruneBeforeAsync(now - KeepClaims, ct);

        logger.LogInformation(
            "Reminders tick: sent {Sent}, skipped {Skipped}, retried {Retried}, failed {Failed}",
            tally.Sent, tally.Skipped, tally.Retried, tally.Failed);

        if (faults.Count > 0)
        {
            throw new AggregateException(faults);
        }

        return new TickResult(tally.Sent, tally.Skipped, tally.Retried, tally.Failed);
    }

    private static async Task IsolatedAsync(Func<Task> work, List<Exception> faults, CancellationToken ct)
    {
        try
        {
            await work();
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            faults.Add(e);
        }
    }

    private async Task RemindAsync(
        TodoTask task, Recipient recipient, TimeZoneInfo zone, ReminderMoment moment, DateTimeOffset now, Tally tally,
        CancellationToken ct)
    {
        var occurrence = moment.Occurrence;
        var devices = await subscriptions.ForUserAsync(recipient.UserId, ct);
        if (devices.Count == 0)
        {
            tally.Skipped++;
            return;
        }

        var note = ReminderTexts.For(task, task.Reminder!.DaysBefore, recipient.Language, settings.AppBaseAddress);
        var ttl = ReminderTtl.For(occurrence, zone, now);
        var claim = new ReminderClaim(
            ReminderClaim.IdFor(task.Id, occurrence, recipient.UserId, moment.Moment), task.Id, occurrence,
            recipient.UserId, now, 1, ClaimState.Sent, note, now + ttl);

        if (!await claims.TryClaimAsync(claim, ct))
        {
            tally.Skipped++;
            return;
        }

        var state = await DeliverAsync(claim, devices, 1, now, ct);
        if (state == ClaimState.Failed)
        {
            tally.Failed++;
        }
        else if (state == ClaimState.Sent)
        {
            tally.Sent++;
        }
    }

    private async Task RetryAsync(ReminderClaim claim, DateTimeOffset now, Tally tally, CancellationToken ct)
    {
        if (claim.ClaimedAt >= now)
        {
            return;
        }

        if (claim.ExpiresAt <= now || claim.Attempts >= MostAttempts)
        {
            await claims.UpdateAsync(claim.Id, ClaimState.Failed, claim.Attempts, ct);
            tally.Failed++;
            return;
        }

        var missed = (await subscriptions.ForUserAsync(claim.UserId, ct))
            .Where(s => s.LastSuccessAt is null || s.LastSuccessAt < claim.ClaimedAt)
            .ToList();
        var attempts = claim.Attempts + 1;
        var state = await DeliverAsync(claim, missed, attempts, now, ct);
        tally.Retried++;

        if (state == ClaimState.Retry && attempts >= MostAttempts)
        {
            await claims.UpdateAsync(claim.Id, ClaimState.Failed, attempts, ct);
            tally.Failed++;
        }
        else if (state == ClaimState.Failed)
        {
            tally.Failed++;
        }
    }

    private async Task<ClaimState> DeliverAsync(
        ReminderClaim claim, IReadOnlyList<PushSubscriptionRecord> devices, int attempts, DateTimeOffset now,
        CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(Math.Clamp((claim.ExpiresAt - now).TotalSeconds, 60, 86400));
        bool anyRetry = false, anyFailed = false, anyDelivered = false;

        foreach (var device in devices)
        {
            var outcome = await SendAsync(device, claim.Note, ttl, ct);
            switch (outcome)
            {
                case PushOutcome.Delivered:
                    anyDelivered = true;
                    await subscriptions.MarkDeliveredAsync(device.Endpoint, now, ct);
                    break;
                case PushOutcome.Gone:
                    await subscriptions.DeleteAsync(device.Endpoint, ct);
                    break;
                case PushOutcome.Retry:
                    anyRetry = true;
                    break;
                case PushOutcome.Failed:
                    anyFailed = true;
                    break;
            }
        }

        if (anyRetry)
        {
            await claims.UpdateAsync(claim.Id, ClaimState.Retry, attempts, ct);
            return ClaimState.Retry;
        }

        if (anyFailed && !anyDelivered)
        {
            await claims.UpdateAsync(claim.Id, ClaimState.Failed, attempts, ct);
            return ClaimState.Failed;
        }

        if (attempts > 1)
        {
            await claims.UpdateAsync(claim.Id, ClaimState.Sent, attempts, ct);
        }

        return ClaimState.Sent;
    }

    private async Task<PushOutcome> SendAsync(
        PushSubscriptionRecord device, PushNote note, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            return PushAnswer.Of(await sender.SendAsync(device, note, ttl, ct));
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Sending a reminder to a device threw {ExceptionType}", e.GetType().Name);
            return PushOutcome.Failed;
        }
    }

    private static TimeZoneInfo ZoneOf(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
