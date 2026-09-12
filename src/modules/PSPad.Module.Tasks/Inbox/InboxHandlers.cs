using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

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

public sealed class OrganiseInboxItemHandler(
    IDocumentStore<Inbox> inboxes,
    IDocumentStore<TodoTask> tasks,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<OrganiseInboxItem>
{
    public async Task<CommandResult> HandleAsync(OrganiseInboxItem command, CancellationToken ct)
    {
        var inbox = await inboxes.LoadAsync(command.InboxId, ct);

        try
        {
            var item = inbox?.Items.FirstOrDefault(candidate => candidate.Id == command.ItemId)
                ?? throw new DomainRejectedException("That item is no longer in your inbox.");

            var existingTask = await tasks.LoadAsync(command.TaskId, ct);
            var taskEvents = TodoTask.Decide(
                existingTask,
                new CreateTask(command.CommandId, command.UserId, command.TaskId, command.ListId, item.Text),
                clock.UtcNow);
            var task = existingTask ?? new TodoTask();
            task.ApplyAll(taskEvents);

            var inboxEvents = Inbox.Decide(inbox, command, clock.UtcNow);
            inbox!.ApplyAll(inboxEvents);

            work.Stage(task, taskEvents);
            work.Stage(inbox, inboxEvents);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}

public sealed class DiscardInboxItemHandler(IDocumentStore<Inbox> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<DiscardInboxItem>
{
    public async Task<CommandResult> HandleAsync(DiscardInboxItem command, CancellationToken ct)
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
