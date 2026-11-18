namespace PSPad.Module.Reminders;

public static class PushAnswer
{
    public static PushOutcome Of(int? statusCode) => statusCode switch
    {
        null or 429 or >= 500 => PushOutcome.Retry,
        >= 200 and < 300 => PushOutcome.Delivered,
        404 or 410 => PushOutcome.Gone,
        _ => PushOutcome.Failed,
    };
}
