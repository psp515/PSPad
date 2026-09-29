using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed class MoveReferenceFieldHandler(IDocumentStore<ReferenceItem> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<MoveReferenceField>
{
    public async Task<CommandResult> HandleAsync(MoveReferenceField command, CancellationToken ct)
    {
        var item = await store.LoadAsync(command.ItemId, ct);

        try
        {
            var events = ReferenceItem.Decide(item, command, clock.UtcNow);
            item!.ApplyAll(events);
            work.Stage(item, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
