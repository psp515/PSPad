using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Tests.Persistence;

public sealed class ThrowingDomainEventHandler(string taskName) : IDomainEventHandler
{
    readonly TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Attempted => _attempted.Task;

    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        if (envelope.Event is TaskCreated created && created.Name == taskName)
        {
            _attempted.TrySetResult();
            throw new InvalidOperationException("projection is broken");
        }

        return Task.CompletedTask;
    }
}
