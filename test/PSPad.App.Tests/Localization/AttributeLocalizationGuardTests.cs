using System.Text.RegularExpressions;
using PSPad.App.Tests.Hosting;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
public partial class AttributeLocalizationGuardTests
{
    [GeneratedRegex("""\b(?:aria-[a-z]+|title|alt|role|data-[a-z-]+)="@(?:\((?:.*?(?<!\?)[?:](?!\?)\s*)?)?[A-Za-z]+\[(?:[^\]"]|"[^"]*")*\](?!\.Value)""")]
    private static partial Regex LocalizedStringInUnmatchedAttribute();

    [Fact]
    public void UnmatchedAttributesReceiveLocalizedValuesNotLocalizedStringObjects()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src", "PSPad.App"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (line, number: index + 1))
                .Where(entry => LocalizedStringInUnmatchedAttribute().IsMatch(entry.line))
                .Select(entry => $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{entry.number}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Add .Value to localized attribute values: " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("aria-label=\"@T[\"X\"]\"", true)]
    [InlineData("title=\"@(cond ? T[\"A\"] : T[\"B\"])\"", true)]
    [InlineData("aria-label=\"@(T[\"X\"])\"", true)]
    [InlineData("aria-label=\"@(c ? T[\"A\"].Value : T[\"B\"])\"", true)]
    [InlineData("aria-label=\"@T[\"X\"].Value\"", false)]
    [InlineData("Placeholder=\"@T[\"X\"]\"", false)]
    [InlineData("aria-selected=\"@(c ? \"true\" : \"false\")\"", false)]
    public void TheGuardPatternFlagsOnlyLocalizedStringObjectsInUnmatchedAttributes(string line, bool caught)
    {
        Assert.Equal(caught, LocalizedStringInUnmatchedAttribute().IsMatch(line));
    }
}
