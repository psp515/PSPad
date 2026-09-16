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

        var work = services.GetService<IUnitOfWork>();
        if (work is not null && await work.IsProcessedAsync(command.CommandId, ct))
        {
            // Checked here, not in Decide: by replay time the aggregate already reflects the
            // first application, so the domain layer alone cannot tell a replay from a genuine conflict.
            return new CommandResponse(command.CommandId, true, null);
        }

        var handler = services.GetService(typeof(ICommandHandler<>).MakeGenericType(type));
        if (handler is null)
        {
            return new CommandResponse(command.CommandId, false, $"No handler for {envelope.Type}.", Unrecoverable: true);
        }

        var method = handler.GetType().GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
        var result = await (Task<CommandResult>)method.Invoke(handler, [command, ct])!;

        return new CommandResponse(command.CommandId, result.Accepted, result.Rejection);
    }
}
