using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.References;

public sealed class CreateReferenceItemHandler(
    IDocumentStore<ReferenceItem> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<CreateReferenceItem>
{
    public async Task<CommandResult> HandleAsync(CreateReferenceItem command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.ItemId, ct);

        try
        {
            var access = TaskList.RequireAcceptsReferences(await lists.LoadAsync(command.ListId, ct), command.UserId);
            var events = ReferenceItem.Decide(existing, command, clock.UtcNow, access);
            var item = existing ?? new ReferenceItem();
            item.ApplyAll(events);
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
