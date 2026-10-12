namespace PSPad.Backup.Processes;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string program, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, CancellationToken ct);
}
