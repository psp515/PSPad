using PSPad.Backup.Archives;
using PSPad.Backup.Configuration;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Archives;

[UnitTest]
public class RetentionPolicyTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 12, 3, 17, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyThisStacksBackupsOlderThanTheWindowExpire()
    {
        string[] stored =
        [
            "mongo-2026-09-27T031700Z.tar",
            "mongo-2026-09-28T031700Z.tar",
            "mongo-2026-10-12T031700Z.tar",
            "keycloak-2026-01-01T000000Z.tar",
            "keep-me.tar"
        ];

        var expired = RetentionPolicy.Expired(BackupStack.Mongo, stored, Now, keepDays: 14);

        Assert.Equal(["mongo-2026-09-27T031700Z.tar"], expired);
    }
}
