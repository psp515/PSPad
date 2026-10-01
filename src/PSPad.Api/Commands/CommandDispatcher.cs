using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Contracts;

namespace PSPad.Api.Commands;

public sealed class CommandDispatcher(IServiceProvider services)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CommandResponse> DispatchAsync(
        CommandEnvelope envelope, Guid userId, CancellationToken ct)
    {
        var type = CommandCatalogue.Resolve(envelope.Type);
        if (type is null)
        {
            return new CommandResponse(Guid.Empty, false, $"Unknown command {envelope.Type}.", Unrecoverable: true);
        }

        if (envelope.Payload.Deserialize(type, Json) is not ICommand command)
        {
            return new CommandResponse(Guid.Empty, false, $"Malformed payload for {envelope.Type}.", Unrecoverable: true);
        }

        if (command.UserId != userId)
        {
            return new CommandResponse(command.CommandId, false, "That command is for a different user.", Unrecoverable: true);
        }

        if (command is IServerOnlyCommand)
        {
            return new CommandResponse(command.CommandId, false, $"{envelope.Type} cannot be sent from a client.", Unrecoverable: true);
        }

        if (services.GetService(typeof(ICommandHandler<>).MakeGenericType(type)) is null)
        {
            return new CommandResponse(command.CommandId, false, $"No handler for {envelope.Type}.", Unrecoverable: true);
        }

        var result = await RunAsync(command, ct);
        return new CommandResponse(command.CommandId, result.Accepted, result.Rejection);
    }

    public async Task<CommandResult> RunAsync(ICommand command, CancellationToken ct)
    {
        var work = services.GetService<IUnitOfWork>();
        if (work is not null && await work.IsProcessedAsync(command.CommandId, ct))
        {
            // Checked here, not in Decide: by replay time the aggregate already reflects the
            // first application, so the domain layer alone cannot tell a replay from a genuine conflict.
            return CommandResult.Ok();
        }

        var handler = services.GetService(typeof(ICommandHandler<>).MakeGenericType(command.GetType()));
        if (handler is null)
        {
            return CommandResult.Rejected($"No handler for {command.GetType().Name}.");
        }

        var method = handler.GetType().GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
        return await (Task<CommandResult>)method.Invoke(handler, [command, ct])!;
    }
}
