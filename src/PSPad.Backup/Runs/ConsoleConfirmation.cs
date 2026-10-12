namespace PSPad.Backup.Runs;

public sealed class ConsoleConfirmation : IConfirmation
{
    public bool Confirm(string word, string backup)
    {
        if (Console.IsInputRedirected)
        {
            throw new BackupFailure($"a restore overwrites {word}; pass --yes, or run it with a terminal");
        }

        Console.Error.Write($"This replaces {word} with {backup}. Type '{word}' to continue: ");
        return Console.ReadLine()?.Trim() == word;
    }
}
