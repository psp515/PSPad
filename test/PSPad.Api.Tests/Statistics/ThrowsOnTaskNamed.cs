using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Tests.Statistics;

public sealed class ThrowsOnTaskNamed(string name, Guid userId) : IDomainEventHandler
{
    readonly TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Attempted => _attempted.Task;

    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        if (envelope.Event is TaskCreated created && created.UserId == userId && created.Name == name)
        {
            _attempted.TrySetResult();
            throw new InvalidOperationException("projection failed");
        }

        return Task.CompletedTask;
    }
}
