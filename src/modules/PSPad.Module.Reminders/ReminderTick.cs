using Microsoft.Extensions.Logging;
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
        int sent = 0, skipped = 0, retried = 0, failed = 0;

        foreach (var candidate in await source.CandidatesAsync(ct))
        {
            var task = candidate.Task;
            var reminder = task.Reminder;
            if (reminder is null)
            {
                continue;
            }

            foreach (var recipient in candidate.Recipients)
            {
                var zone = ZoneOf(recipient.TimeZone);

                foreach (var moment in ReminderRule.MomentsBetween(task, zone, from, now))
                {
                    var devices = await subscriptions.ForUserAsync(recipient.UserId, ct);
                    if (devices.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    var note = ReminderTexts.For(task, reminder.DaysBefore, recipient.Language, settings.AppBaseAddress);
                    var ttl = ReminderTtl.For(moment.Occurrence, zone, now);
                    var claim = new ReminderClaim(
                        ReminderClaim.IdFor(task.Id, moment.Occurrence, recipient.UserId), task.Id, moment.Occurrence,
                        recipient.UserId, now, 1, ClaimState.Sent, note, now + ttl);

                    if (!await claims.TryClaimAsync(claim, ct))
                    {
                        skipped++;
                        continue;
                    }

                    var state = await DeliverAsync(claim, devices, 1, now, ct);
                    if (state == ClaimState.Failed)
                    {
                        failed++;
                    }
                    else if (state == ClaimState.Sent)
                    {
                        sent++;
                    }
                }
            }
        }

        foreach (var claim in await claims.RetriesSinceAsync(now - CatchUp, ct))
        {
            if (claim.ClaimedAt >= now)
            {
                continue;
            }

            if (claim.ExpiresAt <= now || claim.Attempts >= MostAttempts)
            {
                await claims.UpdateAsync(claim.Id, ClaimState.Failed, claim.Attempts, ct);
                failed++;
                continue;
            }

            var missed = (await subscriptions.ForUserAsync(claim.UserId, ct))
                .Where(s => s.LastSuccessAt is null || s.LastSuccessAt < claim.ClaimedAt)
                .ToList();
            await DeliverAsync(claim, missed, claim.Attempts + 1, now, ct);
            retried++;
        }

        await claims.PruneBeforeAsync(now - KeepClaims, ct);

        logger.LogInformation(
            "Reminders tick: sent {Sent}, skipped {Skipped}, retried {Retried}, failed {Failed}",
            sent, skipped, retried, failed);
        return new TickResult(sent, skipped, retried, failed);
    }

    private async Task<ClaimState> DeliverAsync(
        ReminderClaim claim, IReadOnlyList<PushSubscriptionRecord> devices, int attempts, DateTimeOffset now,
        CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(Math.Clamp((claim.ExpiresAt - now).TotalSeconds, 60, 86400));
        bool anyRetry = false, anyFailed = false, anyDelivered = false;

        foreach (var device in devices)
        {
            var outcome = PushAnswer.Of(await sender.SendAsync(device, claim.Note, ttl, ct));
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
