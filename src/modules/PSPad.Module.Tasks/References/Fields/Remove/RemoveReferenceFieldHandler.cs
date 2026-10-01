using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.References;

public sealed class RemoveReferenceFieldHandler(
    IDocumentStore<ReferenceItem> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<RemoveReferenceField>
{
    public async Task<CommandResult> HandleAsync(RemoveReferenceField command, CancellationToken ct)
    {
        var item = await store.LoadAsync(command.ItemId, ct);

        try
        {
            var access = await lists.AccessAsync(item?.ListId, command.UserId, ct);
            var events = ReferenceItem.Decide(item, command, clock.UtcNow, access);
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
