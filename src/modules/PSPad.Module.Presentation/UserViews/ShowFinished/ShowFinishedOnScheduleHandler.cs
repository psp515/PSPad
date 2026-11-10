using PSPad.Abstractions;

namespace PSPad.Module.Presentation.UserViews;

public sealed class ShowFinishedOnScheduleHandler(IDocumentStore<UserView> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<ShowFinishedOnSchedule>
{
    public async Task<CommandResult> HandleAsync(ShowFinishedOnSchedule command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(UserView.IdFor(command.UserId), ct);

        try
        {
            var events = UserView.Decide(existing, command, clock.UtcNow);
            if (events.Count == 0)
            {
                return CommandResult.Ok();
            }

            var view = existing ?? new UserView();
            view.ApplyAll(events);
            work.Stage(view, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection);
        }
    }
}
