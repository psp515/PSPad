using System.Text.RegularExpressions;
using System.Xml.Linq;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Deployment;

[UnitTest]
public partial class CentralPackageTests
{
    static readonly string Root = RepositoryFiles.Root;

    [Fact]
    public void VersionsAreManagedCentrally()
    {
        var props = XDocument.Load(Path.Combine(Root, "Directory.Packages.props"));

        Assert.Equal("true", props.Descendants("ManagePackageVersionsCentrally").Single().Value);
    }

    [Fact]
    public void NoProjectPinsItsOwnPackageVersion()
    {
        var projects = new[] { "src", "test" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder), "*.csproj", SearchOption.AllDirectories));

        Assert.All(projects, project => Assert.DoesNotMatch(VersionedReference(), File.ReadAllText(project)));
    }

    [Fact]
    public void EveryFrameworkPackageIsOnTheSameRelease()
    {
        var versions = XDocument.Load(Path.Combine(Root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Where(package => package.Attribute("Include")!.Value.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal)
                || package.Attribute("Include")!.Value.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
            .Select(package => package.Attribute("Version")!.Value)
            .Distinct();

        Assert.Single(versions);
    }

    [GeneratedRegex(@"<PackageReference\b[^>]*\sVersion\s*=")]
    private static partial Regex VersionedReference();
}
