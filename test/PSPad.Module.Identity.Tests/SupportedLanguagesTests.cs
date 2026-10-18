using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Identity.Tests;

[UnitTest]
public class SupportedLanguagesTests
{
    [Theory]
    [InlineData("pl-PL", "pl")]
    [InlineData("PL", "pl")]
    [InlineData("pl", "pl")]
    [InlineData("en-GB", "en")]
    [InlineData("de", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void FromBrowserMapsThePrimarySubtag(string? browser, string expected)
    {
        Assert.Equal(expected, SupportedLanguages.FromBrowser(browser));
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("pl", true)]
    [InlineData("de", false)]
    [InlineData("pl-PL", false)]
    [InlineData(null, false)]
    public void OnlyExactTagsAreSupported(string? tag, bool expected)
    {
        Assert.Equal(expected, SupportedLanguages.IsSupported(tag));
    }

    [Fact]
    public void RequireRejectsAnUnsupportedTag()
    {
        Assert.Equal("pl", SupportedLanguages.Require("pl"));
        Assert.Throws<DomainRejectedException>(() => SupportedLanguages.Require("de"));
    }
}
