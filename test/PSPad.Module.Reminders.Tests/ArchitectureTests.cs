using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void RemindersCarryNoInfrastructure() =>
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(RemindersModuleMarker).Assembly,
            "MongoDB", "Microsoft.AspNetCore", "System.Net.Http", "Microsoft.Extensions.Hosting",
            "PSPad.Infrastructure");

    [Fact]
    public void RemindersKnowsOnlyTasksAndIdentity() =>
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(RemindersModuleMarker).Assembly,
            "PSPad.Module.Statistics", "PSPad.Module.Presentation", "PSPad.Module.Sharing", "PSPad.Module.Money",
            "PSPad.Contracts");
}
