using System.Diagnostics;

namespace PSPad.Backup.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string program, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, CancellationToken ct)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[key] = value;
        }

        using var process = Process.Start(start) ?? throw new BackupFailure($"{program} could not be started");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var result = new ProcessResult(await output, await errors);
        if (process.ExitCode != 0)
        {
            throw new BackupFailure($"{program} failed: {LastLines(result.Errors + result.Output)}");
        }

        return result;
    }

    static string LastLines(string text) =>
        string.Join(" | ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(3));
}
