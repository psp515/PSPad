using System.Text.RegularExpressions;

namespace PSPad.TestInfrastructure;

public static partial class EndToEndLocatorGuard
{
    const string AllowlistFile = "locator-allowlist.txt";

    static readonly string[] ScannedFolders = ["scenarios", "helpers"];

    [GeneratedRegex(@"\.(pspad|mud)-|['""`](pspad|mud)-|\[class[~*^$|]?=|getAttribute\(\s*['""`]class['""`]|\btoHaveClass\(|\bclassList\b")]
    private static partial Regex ClassLocator();

    public static void AssertLocatorsAreAccessible() => AssertLocatorsAreAccessible(EndToEndDirectory());

    public static void AssertLocatorsAreAccessible(string endToEndDirectory)
    {
        var allowed = ReadAllowlist(endToEndDirectory);
        var lines = ScannedFiles(endToEndDirectory)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (File: RelativePath(endToEndDirectory, path), Line: line, Number: index + 1)))
            .Where(entry => ClassLocator().IsMatch(entry.Line))
            .ToList();

        var offenders = lines
            .Where(entry => ClassLocator().IsMatch(Unallowed(allowed, entry.File, entry.Line)))
            .Select(entry => $"{entry.File}:{entry.Number} locates by a CSS class; use a role, label or text (ADR-0080) or add it to {AllowlistFile} with the reason");
        var stale = allowed
            .Where(allowance => !lines.Any(entry => allowance.Covers(entry.File, entry.Line)))
            .Select(allowance => $"{AllowlistFile}: \"{allowance.File}: {allowance.Pattern}\" matches no class locator; remove it");

        var problems = offenders.Concat(stale).ToList();
        if (problems.Count > 0)
            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, problems));
    }

    static List<string> ScannedFiles(string endToEndDirectory)
    {
        var missing = ScannedFolders.Where(folder => !Directory.Exists(Path.Combine(endToEndDirectory, folder))).ToList();
        if (missing.Count > 0)
            throw new Xunit.Sdk.XunitException($"{endToEndDirectory} has no {string.Join(" or ", missing)} folder to scan");

        var files = ScannedFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(endToEndDirectory, folder), "*.ts", SearchOption.AllDirectories))
            .ToList();
        if (files.Count == 0)
            throw new Xunit.Sdk.XunitException($"{endToEndDirectory} has no .ts files to scan");

        return files;
    }

    static string Unallowed(IEnumerable<Allowance> allowed, string file, string line) =>
        allowed.Where(allowance => allowance.File == file)
            .Aggregate(line, (rest, allowance) => rest.Replace(allowance.Pattern, " ", StringComparison.Ordinal));

    static IReadOnlyList<Allowance> ReadAllowlist(string endToEndDirectory)
    {
        var path = Path.Combine(endToEndDirectory, AllowlistFile);
        if (!File.Exists(path))
            return [];

        return File.ReadLines(path)
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            .Select(Allowance.Parse)
            .ToList();
    }

    static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    static string EndToEndDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PSPad.slnx")))
            directory = directory.Parent;
        if (directory is null)
            throw new Xunit.Sdk.XunitException($"No PSPad.slnx above {AppContext.BaseDirectory}; cannot find test/e2e");
        return Path.Combine(directory.FullName, "test", "e2e");
    }

    sealed record Allowance(string File, string Pattern, string Reason)
    {
        public static Allowance Parse(string line)
        {
            var fileEnd = line.IndexOf(": ", StringComparison.Ordinal);
            var reasonStart = line.IndexOf(" — ", StringComparison.Ordinal);
            if (fileEnd < 0 || reasonStart < fileEnd || line[(reasonStart + 3)..].Trim().Length == 0)
                throw new Xunit.Sdk.XunitException($"{AllowlistFile}: \"{line}\" is not \"file: line pattern — reason\"");

            return new Allowance(line[..fileEnd].Trim(), line[(fileEnd + 2)..reasonStart].Trim(), line[(reasonStart + 3)..].Trim());
        }

        public bool Covers(string file, string line) => file == File && line.Contains(Pattern, StringComparison.Ordinal);
    }
}
