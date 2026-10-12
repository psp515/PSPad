namespace PSPad.Backup;

public sealed class BackupLog(TextWriter writer)
{
    public void Info(string message) => writer.WriteLine($"backup: {message}");
}
