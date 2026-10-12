using System.Globalization;

namespace PSPad.Backup.Health;

public sealed class HealthStamps(string startedFile, string successFile, TimeProvider clock)
{
    static readonly TimeSpan Window = TimeSpan.FromHours(26);

    public void MarkStarted() => Stamp(startedFile);

    public void MarkSucceeded() => Stamp(successFile);

    public bool IsHealthy()
    {
        var now = clock.GetUtcNow();
        if (Read(successFile) is { } success)
        {
            return now - success < Window;
        }

        return Read(startedFile) is { } started && now - started < Window;
    }

    void Stamp(string file) =>
        File.WriteAllText(file, clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

    static DateTimeOffset? Read(string file) =>
        File.Exists(file) && DateTimeOffset.TryParse(File.ReadAllText(file).Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;
}
