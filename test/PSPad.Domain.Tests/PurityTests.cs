using PSPad.Contracts;
using PSPad.TestInfrastructure;
using Shouldly;

namespace PSPad.Domain.Tests;

[UnitTest]
public class PurityTests
{
    static readonly string[] ForbiddenPrefixes =
    [
        "Marten", "Npgsql", "Wolverine", "Microsoft.AspNetCore", "System.Net.Http"
    ];

    [Theory]
    [InlineData(typeof(DomainMarker))]
    [InlineData(typeof(ContractsMarker))]
    public void Assembly_build_output_contains_no_infrastructure_dlls(Type marker)
    {
        var outputDirectory = Path.GetDirectoryName(marker.Assembly.Location)!;
        var dllNames = Directory.GetFiles(outputDirectory, "*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();

        foreach (var prefix in ForbiddenPrefixes)
        {
            dllNames.ShouldNotContain(
                name => name!.StartsWith(prefix, StringComparison.Ordinal),
                $"{marker.Assembly.GetName().Name} build output must not contain {prefix} (see AD-4)");
        }
    }
}
