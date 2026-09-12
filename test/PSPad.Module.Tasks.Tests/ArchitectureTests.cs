using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class ArchitectureTests
{
    static readonly string[] Infrastructure =
        ["MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure"];

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

    [Fact]
    public void TheModuleNeverReadsMachineLocalTime()
    {
        var source = Directory.EnumerateFiles(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "modules", "PSPad.Module.Tasks"),
            "*.cs", SearchOption.AllDirectories);

        var offenders = source
            .Where(file => File.ReadAllText(file) is var text &&
                (text.Contains("DateTime.Now") || text.Contains("DateTime.Today") ||
                 text.Contains("DateTimeOffset.Now")))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0, $"Machine-local time in: {string.Join(", ", offenders)}");
    }
}
