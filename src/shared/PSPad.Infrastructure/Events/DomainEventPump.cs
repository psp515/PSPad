using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Events;

public sealed class DomainEventPump(
    ChannelDomainEventDispatcher dispatcher,
    IServiceScopeFactory scopes) : BackgroundService
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
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"Handler {handler.GetType().Name} failed on seq {envelope.Seq}: {exception.Message}");
                }
            }
        }
    }
}
