using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Commands;

public sealed class CommandDispatcher(IServiceProvider services)
{
    public const int MostAttempts = 5;

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CommandResponse> DispatchAsync(
        CommandEnvelope envelope, Guid userId, CancellationToken ct)
    {
        var type = CommandCatalogue.Resolve(envelope.Type);
        if (type is null)
        {
            return new CommandResponse(Guid.Empty, false, $"Unknown command {envelope.Type}.", Unrecoverable: true);
        }

        if (Deserialize(envelope.Payload, type) is not ICommand command)
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

    static object? Deserialize(JsonElement payload, Type type)
    {
        if (payload.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return payload.Deserialize(type, Json);
        }
        catch (JsonException)
        {
            return null;
        }
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

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await (Task<CommandResult>)method.Invoke(handler, [command, ct])!;
            }
            catch (ConcurrencyConflictException) when (attempt < MostAttempts)
            {
                // Jittered so requests that collided once do not retry in lockstep and collide again.
                await Task.Delay(Random.Shared.Next(10, 40) * attempt, ct);
            }
            catch (ConcurrencyConflictException)
            {
                return CommandResult.Rejected("Someone else changed this at the same time. Try again.");
            }
        }
    }
}
