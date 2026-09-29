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
            var events = ReferenceItem.Decide(item, command, clock.UtcNow);
            if (events.Count > 0)
            {
                TaskList.RequireAcceptsReferences(await lists.LoadAsync(command.ListId, ct), command.UserId);
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
