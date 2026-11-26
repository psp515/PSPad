using Microsoft.Extensions.Options;
using PSPad.Abstractions;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class ReminderScheduler(
    IServiceScopeFactory scopes, IClock clock, IOptions<ReminderOptions> options, ILogger<ReminderScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var from = clock.UtcNow - ReminderTick.CatchUp;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            var now = clock.UtcNow;
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReminderTick>().RunAsync(from, now, stoppingToken);
                from = now;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Reminders tick failed; the next tick retries the window");
                from = from < now - ReminderTick.CatchUp ? now - ReminderTick.CatchUp : from;
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
