using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.Tests.Hosting;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
public class ResourceGuardTests
{
    static readonly string Resources = Path.Combine(RepositoryPaths.Root, "src", "PSPad.App", "Resources");

    public static IEnumerable<object[]> EnglishFiles() =>
        Directory.GetFiles(Resources, "*.resx")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains('.'))
            .Select(path => new object[] { Path.GetFileName(path) });

    [Theory]
    [MemberData(nameof(EnglishFiles))]
    public void EveryLanguageHasEveryKeyAndNoEmptyValue(string file)
    {
        var english = Keys(Path.Combine(Resources, file));
        var polish = Keys(Path.Combine(Resources, Path.GetFileNameWithoutExtension(file) + ".pl.resx"));

        Assert.Empty(english.Keys.Except(polish.Keys));
        Assert.Empty(polish.Keys.Except(english.Keys));
        Assert.Empty(english.Concat(polish).Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key));
    }

    [Theory]
    [MemberData(nameof(EnglishFiles))]
    public void PluralKeysComeInCompleteSets(string file)
    {
        var polish = Keys(Path.Combine(Resources, Path.GetFileNameWithoutExtension(file) + ".pl.resx"));
        var stems = polish.Keys.Where(key => key.EndsWith(".One") || key.EndsWith(".Few") || key.EndsWith(".Many"))
            .Select(key => key[..key.LastIndexOf('.')]).Distinct();

        foreach (var stem in stems)
        {
            Assert.Contains(stem + ".One", polish.Keys);
            Assert.Contains(stem + ".Few", polish.Keys);
            Assert.Contains(stem + ".Many", polish.Keys);
        }
    }

    [Fact]
    public void SharedStringsResolveInBothLanguages()
    {
        var strings = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<SharedStrings>>();
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Save", strings["Save"].Value);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal("Zapisz", strings["Save"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    static Dictionary<string, string> Keys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "");
}
