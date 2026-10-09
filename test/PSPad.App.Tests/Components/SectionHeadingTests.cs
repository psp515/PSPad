using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class SectionHeadingTests : Bunit.TestContext
{
    public SectionHeadingTests() => Services.AddMudServices();

    [Fact]
    public void ItRendersTheTitleAsAnH2()
    {
        var heading = Render<SectionHeading>(parameters => parameters.Add(p => p.Title, "Coming up"));

        Assert.Equal("Coming up", heading.Find("h2.pspad-section-heading").TextContent.Trim());
    }

    [Fact]
    public void ACountShowsAsAPill()
    {
        var heading = Render<SectionHeading>(parameters => parameters
            .Add(p => p.Title, "Completed")
            .Add(p => p.Count, 3));

        Assert.Equal("3", heading.Find(".pspad-section-count").TextContent.Trim());
    }

    [Fact]
    public void NoCountMeansNoPill()
    {
        var heading = Render<SectionHeading>(parameters => parameters.Add(p => p.Title, "Coming up"));

        Assert.Empty(heading.FindAll(".pspad-section-count"));
    }

    [Fact]
    public void AHintFollowsTheTitleMuted()
    {
        var heading = Render<SectionHeading>(parameters => parameters
            .Add(p => p.Title, "Starred")
            .Add(p => p.Hint, "· when you have time"));

        var hint = heading.Find(".pspad-section-hint");
        Assert.Equal("· when you have time", hint.TextContent.Trim());
        Assert.Contains("pspad-muted", hint.ClassList);
    }

    [Theory]
    [InlineData(Color.Error, "mud-error-text")]
    [InlineData(Color.Primary, "mud-primary-text")]
    public void TheColourAddsTheMatchingTextClass(Color color, string expected)
    {
        var heading = Render<SectionHeading>(parameters => parameters
            .Add(p => p.Title, "Overdue")
            .Add(p => p.Color, color));

        Assert.Contains(expected, heading.Find("h2").ClassList);
    }
}
