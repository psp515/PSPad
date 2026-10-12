using PSPad.Backup;
using PSPad.Backup.Configuration;
using PSPad.TestInfrastructure;

namespace PSPad.Backup.Tests.Configuration;

[UnitTest]
public class BackupSettingsTests
{
    static Dictionary<string, string?> Mongo(params (string Key, string? Value)[] extra)
    {
        var env = new Dictionary<string, string?>
        {
            ["BACKUP_STACK"] = "mongo",
            ["BACKUP_MONGO_URI"] = "mongodb://mongo:27017",
            ["BACKUP_MONGO_DATABASE"] = "pspad"
        };
        foreach (var (key, value) in extra)
        {
            env[key] = value;
        }

        return env;
    }

    static string FailureOf(Dictionary<string, string?> env) =>
        Assert.Throws<BackupFailure>(() => BackupSettings.Read(env)).Message;

    [Fact]
    public void DefaultsApplyWhenOnlyTheConnectionIsGiven()
    {
        var settings = BackupSettings.Read(Mongo());

        Assert.Equal(BackupStack.Mongo, settings.Stack);
        Assert.Equal(new TimeOnly(3, 17), settings.At);
        Assert.Equal(TimeZoneInfo.Utc.Id, settings.TimeZone.Id);
        Assert.Equal(14, settings.KeepDays);
        Assert.True(settings.RemotePrune);
        Assert.Null(settings.S3);
        Assert.Null(settings.Drive);
        Assert.False(settings.HasRemotes);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("3:17")]
    [InlineData("noon")]
    public void AnImpossibleRunTimeIsRefused(string at) =>
        Assert.Contains("BACKUP_AT", FailureOf(Mongo(("BACKUP_AT", at))));

    [Theory]
    [InlineData("two")]
    [InlineData("0")]
    [InlineData("-3")]
    public void ARetentionThatIsNotAPositiveWholeNumberIsRefused(string days) =>
        Assert.Contains("BACKUP_KEEP_DAYS", FailureOf(Mongo(("BACKUP_KEEP_DAYS", days))));

    [Fact]
    public void AnUnknownTimeZoneIsRefused() =>
        Assert.Contains("BACKUP_TZ", FailureOf(Mongo(("BACKUP_TZ", "Mars/Olympus"))));

    [Fact]
    public void AMongoStackWithoutAConnectionStringIsRefused() =>
        Assert.Contains("BACKUP_MONGO_URI", FailureOf(Mongo(("BACKUP_MONGO_URI", ""))));

    [Fact]
    public void AMongoStackWithoutADatabaseIsRefused() =>
        Assert.Contains("BACKUP_MONGO_DATABASE", FailureOf(Mongo(("BACKUP_MONGO_DATABASE", null))));

    [Fact]
    public void AKeycloakStackWithoutAConnectionStringIsRefused() =>
        Assert.Contains("BACKUP_PG_URI", FailureOf(new Dictionary<string, string?> { ["BACKUP_STACK"] = "keycloak" }));

    [Fact]
    public void AnUnknownStackIsRefused() =>
        Assert.Contains("BACKUP_STACK", FailureOf(new Dictionary<string, string?> { ["BACKUP_STACK"] = "redis" }));

    [Fact]
    public void S3IsOnOnceABucketIsSet()
    {
        var settings = BackupSettings.Read(Mongo(
            ("BACKUP_S3_BUCKET", "backups"), ("BACKUP_S3_ENDPOINT", "http://s3:8333"), ("BACKUP_S3_PREFIX", "pspad/"),
            ("BACKUP_S3_ACCESS_KEY_ID", "key"), ("BACKUP_S3_SECRET_ACCESS_KEY", "secret")));

        Assert.True(settings.HasRemotes);
        Assert.Equal("backups", settings.S3!.Bucket);
        Assert.Equal("pspad/mongo/", settings.S3.KeyPrefix(BackupStack.Mongo));
    }

    [Fact]
    public void DriveIsOnOnceARefreshTokenIsSetAndNeedsItsClient()
    {
        Assert.Contains("BACKUP_GDRIVE_CLIENT_ID", FailureOf(Mongo(("BACKUP_GDRIVE_REFRESH_TOKEN", "token"))));

        var settings = BackupSettings.Read(Mongo(
            ("BACKUP_GDRIVE_REFRESH_TOKEN", "token"), ("BACKUP_GDRIVE_CLIENT_ID", "id"), ("BACKUP_GDRIVE_CLIENT_SECRET", "secret")));

        Assert.Equal("pspad-backups", settings.Drive!.Folder);
    }

    [Fact]
    public void RemotePruningCanBeTurnedOff() =>
        Assert.False(BackupSettings.Read(Mongo(("BACKUP_REMOTE_PRUNE", "false"))).RemotePrune);
}
