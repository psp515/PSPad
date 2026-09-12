using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed class CaptureToInboxHandler(IDocumentStore<Inbox> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CaptureToInbox>
{
    public async Task<CommandResult> HandleAsync(CaptureToInbox command, CancellationToken ct)
    {
        var inbox = await store.LoadAsync(command.InboxId, ct);

        try
        {
            var events = Inbox.Decide(inbox, command, clock.UtcNow);
            inbox!.ApplyAll(events);
            work.Stage(inbox, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
