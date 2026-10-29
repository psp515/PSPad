using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Commands;

public static class ConflictRetry
{
    public const int MostAttempts = 5;

    public static async Task<T> RunAsync<T>(Func<Task<T>> attempt, CancellationToken ct)
    {
        for (var number = 1; ; number++)
        {
            try
            {
                return await attempt();
            }
            catch (ConcurrencyConflictException) when (number < MostAttempts)
            {
                // Jittered so requests that collided once do not retry in lockstep and collide again.
                await Task.Delay(Random.Shared.Next(10, 40) * number, ct);
            }
        }
    }
}
