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

    [Fact]
    public void Domain_assembly_references_no_infrastructure()
    {
        var referenced = typeof(DomainMarker).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();

        foreach (var prefix in ForbiddenPrefixes)
        {
            referenced.ShouldNotContain(
                name => name.StartsWith(prefix, StringComparison.Ordinal),
                $"PSPad.Domain must not reference {prefix} (see AD-4)");
        }
    }
}
