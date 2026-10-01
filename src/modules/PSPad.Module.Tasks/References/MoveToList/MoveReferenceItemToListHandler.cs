using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.References;

public sealed class MoveReferenceItemToListHandler(
    IDocumentStore<ReferenceItem> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<MoveReferenceItemToList>
{
    public async Task<CommandResult> HandleAsync(MoveReferenceItemToList command, CancellationToken ct)
    {
        var item = await store.LoadAsync(command.ItemId, ct);

        try
        {
            var access = await lists.AccessAsync(item?.ListId, command.UserId, ct);
            var events = ReferenceItem.Decide(item, command, clock.UtcNow, access);
            if (events.Count > 0)
            {
                var target = TaskList.RequireAcceptsReferences(await lists.LoadAsync(command.ListId, ct), command.UserId);
                if (target.OwnerId != access.OwnerId)
                {
                    throw new DomainRejectedException("An item can only move between lists of the same owner.");
                }
            }

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
