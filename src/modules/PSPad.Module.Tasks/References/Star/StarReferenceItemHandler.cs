using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed class StarReferenceItemHandler(IDocumentStore<ReferenceItem> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<StarReferenceItem>
{
    public async Task<CommandResult> HandleAsync(StarReferenceItem command, CancellationToken ct)
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
