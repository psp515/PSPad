using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class ArchitectureTests
{
    static readonly string[] Infrastructure =
        ["MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting"];

    [Fact]
    public void TasksModuleCarriesNoInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(TasksModuleMarker).Assembly, Infrastructure);
    }

    [Fact]
    public void AbstractionsCarryNoInfrastructure()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(PSPad.Abstractions.AbstractionsMarker).Assembly, Infrastructure);
    }

    [Fact]
    public void InfrastructureKnowsNoModule()
    {
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(PSPad.Infrastructure.InfrastructureMarker).Assembly, "PSPad.Module");
    }
}
