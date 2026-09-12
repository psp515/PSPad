using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed class CreateInboxHandler(IDocumentStore<Inbox> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CreateInbox>
{
    public async Task<CommandResult> HandleAsync(CreateInbox command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.InboxId, ct);

        try
        {
            var events = Inbox.Decide(existing, command, clock.UtcNow);
            var inbox = existing ?? new Inbox();
            inbox.ApplyAll(events);
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
