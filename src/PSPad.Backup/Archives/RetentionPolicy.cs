using PSPad.Backup.Configuration;

namespace PSPad.Backup.Archives;

public static class RetentionPolicy
{
    public static IReadOnlyList<string> Expired(BackupStack stack, IEnumerable<string> stored, DateTimeOffset now, int keepDays) =>
        stored
            .Where(file => BackupName.TryParse(stack, file, out var name) && name.TakenAt < now.AddDays(-keepDays))
            .Order(StringComparer.Ordinal)
            .ToArray();
}
