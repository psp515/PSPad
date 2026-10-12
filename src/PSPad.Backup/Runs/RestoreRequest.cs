namespace PSPad.Backup.Runs;

public sealed record RestoreRequest(string Backup, string From, bool Confirmed);
