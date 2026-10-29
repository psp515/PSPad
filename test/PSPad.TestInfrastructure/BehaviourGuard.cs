using System.Text.RegularExpressions;

namespace PSPad.TestInfrastructure;

public static partial class BehaviourGuard
{
    [GeneratedRegex(@"\bClassList\b|\bClassName\b|\.Markup\b|\bMarkupMatches\b|(Find|FindAll|QuerySelector|QuerySelectorAll)\(\s*\$?""[^""]*\.[a-z][\w-]*")]
    private static partial Regex ShapeAssertion();

    [GeneratedRegex(@"\bFakeDocumentStore\b|\bFakeUnitOfWork\b|\bInMemory\w*Store\b")]
    private static partial Regex FakeStore();

    public static void AssertNoShapeAssertions(string testProjectName) =>
        AssertNoMatch(ProjectDirectory(testProjectName), ShapeAssertion(), "asserts shape, not behaviour (ADR-0079)");

    public static void AssertNoFakeStores(string testProjectName) =>
        AssertNoMatch(ProjectDirectory(testProjectName), FakeStore(), "drives a handler through a fake store; test it through the API (ADR-0079)");

    static string ProjectDirectory(string testProjectName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PSPad.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "test", testProjectName);
    }

    static void AssertNoMatch(string testProjectDirectory, Regex pattern, string reason)
    {
        var offenders = Directory
            .EnumerateFiles(testProjectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.EndsWith("BehaviourGuardTests.cs"))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, line, number: index + 1))
                .Where(entry => pattern.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetRelativePath(testProjectDirectory, entry.path)}:{entry.number} {reason}")
            .ToList();

        if (offenders.Count > 0)
            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, offenders));
    }
}
