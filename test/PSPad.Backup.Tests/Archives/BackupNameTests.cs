using PSPad.Backup.Archives;
using PSPad.Backup.Configuration;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Archives;

[UnitTest]
public class BackupNameTests
{
    [Fact]
    public void ANameCarriesTheStackAndTheSecondItWasTaken()
    {
        var name = BackupName.For(BackupStack.Keycloak, new DateTimeOffset(2026, 10, 12, 19, 32, 17, TimeSpan.Zero));

        Assert.Equal("keycloak-2026-10-12T193217Z", name.Value);
        Assert.Equal("keycloak-2026-10-12T193217Z.tar", name.Archive);
        Assert.Equal("keycloak-2026-10-12T193217Z.tar.age", name.Encrypted);
    }

    [Theory]
    [InlineData("mongo-2026-10-12T193217Z.tar")]
    [InlineData("mongo-2026-10-12T193217Z.tar.age")]
    [InlineData("mongo-2026-10-12T193217Z")]
    public void AnyFormOfANameParsesBackToItsTime(string file)
    {
        Assert.True(BackupName.TryParse(BackupStack.Mongo, file, out var name));
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 19, 32, 17, TimeSpan.Zero), name.TakenAt);
    }

    [Theory]
    [InlineData("keycloak-2026-10-12T193217Z.tar")]
    [InlineData("keep-me.tar")]
    [InlineData("mongo-yesterday.tar")]
    public void FilesOfOtherStacksOrOtherShapesAreNotBackupsOfThisStack(string file) =>
        Assert.False(BackupName.TryParse(BackupStack.Mongo, file, out _));
}
