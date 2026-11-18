namespace PSPad.Module.Reminders.Tests.Fakes;

public sealed class FakePushSender : IPushSender
{
    private readonly Queue<int?> answers = new();

    public List<string> Endpoints { get; } = [];

    public void Queue(params int?[] statuses)
    {
        foreach (var status in statuses)
        {
            answers.Enqueue(status);
        }
    }

    public Task<int?> SendAsync(PushSubscriptionRecord to, PushNote note, TimeSpan ttl, CancellationToken ct)
    {
        Endpoints.Add(to.Endpoint);
        return Task.FromResult(answers.Count > 0 ? answers.Dequeue() : 201);
    }
}
