using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using PSPad.Backup.Configuration;

namespace PSPad.Backup.Archives;

public sealed record BackupName(BackupStack Stack, DateTimeOffset TakenAt)
{
    const string StampFormat = "yyyy-MM-dd'T'HHmmss'Z'";

    public string Value => $"{Stack.ToString().ToLowerInvariant()}-{TakenAt.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture)}";

    public string Archive => $"{Value}.tar";

    public string Encrypted => $"{Archive}.age";

    public static BackupName For(BackupStack stack, DateTimeOffset now) =>
        new(stack, new DateTimeOffset(now.UtcDateTime.Ticks - now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero));

    public static bool TryParse(BackupStack stack, string file, [NotNullWhen(true)] out BackupName? name)
    {
        name = null;
        var prefix = $"{stack.ToString().ToLowerInvariant()}-";
        var stem = file.EndsWith(".tar.age", StringComparison.Ordinal) ? file[..^8]
            : file.EndsWith(".tar", StringComparison.Ordinal) ? file[..^4]
            : file;

        if (!stem.StartsWith(prefix, StringComparison.Ordinal) ||
            !DateTime.TryParseExact(stem[prefix.Length..], StampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var taken))
        {
            return false;
        }

        name = new BackupName(stack, new DateTimeOffset(taken, TimeSpan.Zero));
        return true;
    }
}
