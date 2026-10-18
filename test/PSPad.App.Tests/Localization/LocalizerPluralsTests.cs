using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
public class LocalizerPluralsTests
{
    [Fact]
    public void ItPicksTheWholeSentenceForTheForm()
    {
        var strings = new FakeLocalizer(new()
        {
            ["TasksLeft.One"] = "{0} task left",
            ["TasksLeft.Many"] = "{0} tasks left"
        });

        Assert.Equal("1 task left", strings.Plural(1, "TasksLeft"));
        Assert.Equal("3 tasks left", strings.Plural(3, "TasksLeft"));
    }

    [Fact]
    public void ZeroUsesTheZeroSentenceWhenPresent()
    {
        var strings = new FakeLocalizer(new()
        {
            ["TasksLeft.Zero"] = "Nothing left",
            ["TasksLeft.One"] = "{0} task left",
            ["TasksLeft.Many"] = "{0} tasks left"
        });

        Assert.Equal("Nothing left", strings.Plural(0, "TasksLeft"));
    }

    [Fact]
    public void ExtraArgumentsFollowTheCountInTheSentence()
    {
        var strings = new FakeLocalizer(new()
        {
            ["Every.One"] = "Every week on {1}",
            ["Every.Many"] = "Every {0} weeks on {1}"
        });

        Assert.Equal("Every 3 weeks on Mon", strings.Plural(3, "Every", "Mon"));
    }

    [Fact]
    public void AMissingFewFallsBackToMany()
    {
        var strings = new FakeLocalizer(new()
        {
            ["TasksLeft.One"] = "{0} task left",
            ["TasksLeft.Many"] = "{0} tasks left"
        });

        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
        try
        {
            Assert.Equal("3 tasks left", strings.Plural(3, "TasksLeft"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    sealed class FakeLocalizer(Dictionary<string, string> values) : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            values.TryGetValue(name, out var value) ? new(name, value) : new(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            values.Select(pair => new LocalizedString(pair.Key, pair.Value));
    }
}
