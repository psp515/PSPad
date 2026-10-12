using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests;

[UnitTest]
public class ArchitectureTests
{
    [Fact]
    public void TheWorkerKnowsNoModule() =>
        AssemblyReferenceGuard.AssertReferencesNone(
            typeof(BackupCli).Assembly,
            "PSPad.Module.", "PSPad.Api", "PSPad.App", "Microsoft.AspNetCore");
}
