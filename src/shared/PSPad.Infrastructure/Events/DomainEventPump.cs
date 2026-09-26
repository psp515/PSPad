using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Events;

public sealed class DomainEventPump(
    ChannelDomainEventDispatcher dispatcher,
    IServiceScopeFactory scopes,
    ILogger<DomainEventPump> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var replayScope = scopes.CreateScope())
        {
            await replayScope.ServiceProvider
                .GetRequiredService<IDomainEventReplay>()
                .CatchUpAsync(stoppingToken);
        }

        await foreach (var envelope in dispatcher.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();

            foreach (var handler in scope.ServiceProvider.GetServices<IDomainEventHandler>())
            {
                try
                {
                    await handler.HandleAsync(envelope, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(
                        exception,
                        "Handler {Handler} failed on seq {Seq}; the projection marker stays behind it, so the next start replays it.",
                        handler.GetType().Name,
                        envelope.Seq);
                }
            }
        }
    }
}
