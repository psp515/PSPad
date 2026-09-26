using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoUnitOfWork(
    MongoContext context, IDomainEventDispatcher dispatcher, ILogger<MongoUnitOfWork> logger)
    : IUnitOfWork
{
    readonly List<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> _staged = [];
    readonly SequenceSource _sequence = new(context);

    public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) =>
        _staged.Add((aggregate, events));

    public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) =>
        context.Collection<ProcessedCommand>("processed_commands")
            .Find(Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId))
            .AnyAsync(ct);

    public async Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
    {
        if (_staged.Count == 0)
        {
            return;
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        List<DomainEventEnvelope> published = [];

        try
        {
            var processed = context.Collection<ProcessedCommand>("processed_commands");
            var already = await processed
                .Find(session, Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId))
                .AnyAsync(ct);

            if (already)
            {
                await session.AbortTransactionAsync(ct);
                return;
            }

            var events = context.Collection<StoredEvent>("events");

            foreach (var (aggregate, staged) in _staged)
            {
                foreach (var @event in staged)
                {
                    var seq = await _sequence.NextAsync(session, ct);
                    aggregate.Seq = seq;
                    published.Add(new DomainEventEnvelope(seq, @event));
                    await events.InsertOneAsync(session, new StoredEvent
                    {
                        Seq = seq,
                        UserId = @event.UserId,
                        AggregateType = aggregate.GetType().Name,
                        AggregateId = @event.AggregateId,
                        Type = @event.GetType().Name,
                        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
                        At = @event.At
                    }, cancellationToken: ct);
                }

                await ReplaceAsync(session, aggregate, ct);
            }

            await processed.InsertOneAsync(session, new ProcessedCommand
            {
                Id = commandId,
                UserId = userId,
                At = DateTimeOffset.UtcNow
            }, cancellationToken: ct);

            await session.CommitTransactionAsync(ct);

            try
            {
                await dispatcher.PublishAsync(published, ct);
            }
            catch (Exception exception)
            {
                // The transaction already committed; a dispatch failure must never fail an accepted command.
                logger.LogError(
                    exception,
                    "Publishing {Count} committed domain events failed for command {CommandId}; the next start replays them from the projection marker.",
                    published.Count,
                    commandId);
            }
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
        finally
        {
            _staged.Clear();
            published.Clear();
        }
    }

    Task ReplaceAsync(IClientSessionHandle session, Aggregate aggregate, CancellationToken ct)
    {
        var collection = context.Database.GetCollection<Aggregate>(MongoContext.NameOf(aggregate.GetType()));
        return collection.ReplaceOneAsync(
            session,
            Builders<Aggregate>.Filter.Eq(document => document.Id, aggregate.Id),
            aggregate,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}
