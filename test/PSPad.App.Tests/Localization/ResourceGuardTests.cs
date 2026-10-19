using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using PSPad.Abstractions;
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

    public static IEnumerable<object[]> TranslatedFiles() =>
        EnglishFiles().SelectMany(
            english => SupportedLanguages.All.Where(tag => tag != SupportedLanguages.Default),
            (english, tag) => new object[] { english[0], tag });

    [Theory]
    [MemberData(nameof(TranslatedFiles))]
    public void EveryLanguageHasEveryKeyAndNoEmptyValue(string file, string tag)
    {
        var english = Keys(Path.Combine(Resources, file));
        var translated = Keys(Path.Combine(Resources, Path.GetFileNameWithoutExtension(file) + "." + tag + ".resx"));

        Assert.Empty(english.Keys.Except(translated.Keys));
        Assert.Empty(translated.Keys.Except(english.Keys));
        Assert.Empty(english.Concat(translated).Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key));
    }

    [Theory]
    [MemberData(nameof(TranslatedFiles))]
    public void PluralKeysComeInCompleteSets(string file, string tag)
    {
        var translated = Keys(Path.Combine(Resources, Path.GetFileNameWithoutExtension(file) + "." + tag + ".resx"));
        var stems = translated.Keys.Where(key => key.EndsWith(".One") || key.EndsWith(".Few") || key.EndsWith(".Many"))
            .Select(key => key[..key.LastIndexOf('.')]).Distinct();

        foreach (var stem in stems)
        {
            Assert.Contains(stem + ".One", translated.Keys);
            Assert.Contains(stem + ".Few", translated.Keys);
            Assert.Contains(stem + ".Many", translated.Keys);
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
