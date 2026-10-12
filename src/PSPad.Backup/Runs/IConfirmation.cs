namespace PSPad.Backup.Runs;

public interface IConfirmation
{
    bool Confirm(string word, string backup);
}
