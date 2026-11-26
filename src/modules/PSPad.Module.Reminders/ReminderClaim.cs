namespace PSPad.Module.Reminders;

public sealed record ReminderClaim(
    string Id,
    Guid TaskId,
    DateOnly Occurrence,
    Guid UserId,
    DateTimeOffset ClaimedAt,
    int Attempts,
    ClaimState State,
    PushNote Note,
    DateTimeOffset ExpiresAt)
{
    public static string IdFor(Guid taskId, DateOnly occurrence, Guid userId, DateTimeOffset moment) =>
        $"{taskId:N}:{occurrence:yyyy-MM-dd}:{userId:N}:{moment.UtcDateTime:yyyyMMddTHHmm}Z";
}
