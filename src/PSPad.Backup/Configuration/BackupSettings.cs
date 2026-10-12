using System.Globalization;

namespace PSPad.Backup.Configuration;

public sealed record BackupSettings(
    BackupStack Stack,
    TimeOnly At,
    TimeZoneInfo TimeZone,
    int KeepDays,
    bool RemotePrune,
    string? AgeRecipient,
    string? MongoUri,
    string? MongoDatabase,
    string? PostgresUri,
    string? ServiceProbe,
    string? PingUrl,
    S3Settings? S3,
    DriveSettings? Drive)
{
    public const string BackupDirectory = "/backups";
    public const string IdentityFile = "/run/secrets/age-identity";
    public const string StartedStamp = "/tmp/.backup-started";
    public const string SuccessStamp = BackupDirectory + "/.last-success";

    public bool HasRemotes => S3 is not null || Drive is not null;

    public string StackName => Stack.ToString().ToLowerInvariant();

    public static BackupSettings Read(IReadOnlyDictionary<string, string?> env)
    {
        string? Value(string key) => env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        var stack = Value("BACKUP_STACK") switch
        {
            "mongo" => BackupStack.Mongo,
            "keycloak" => BackupStack.Keycloak,
            var other => throw new BackupFailure($"BACKUP_STACK must be mongo or keycloak, got '{other}'")
        };

        var at = Value("BACKUP_AT") ?? "03:17";
        if (!TimeOnly.TryParseExact(at, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new BackupFailure($"BACKUP_AT must be HH:mm between 00:00 and 23:59, got '{at}'");
        }

        var zone = Value("BACKUP_TZ") ?? "UTC";
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var timeZone))
        {
            throw new BackupFailure($"BACKUP_TZ must be an IANA time zone such as Europe/Warsaw, got '{zone}'");
        }

        var keep = Value("BACKUP_KEEP_DAYS") ?? "14";
        if (!int.TryParse(keep, NumberStyles.None, CultureInfo.InvariantCulture, out var keepDays) || keepDays < 1)
        {
            throw new BackupFailure($"BACKUP_KEEP_DAYS must be a whole number of days, got '{keep}'");
        }

        var mongoUri = Value("BACKUP_MONGO_URI");
        var mongoDatabase = Value("BACKUP_MONGO_DATABASE");
        var postgresUri = Value("BACKUP_PG_URI");
        if (stack == BackupStack.Mongo)
        {
            _ = mongoUri ?? throw new BackupFailure("BACKUP_MONGO_URI is not set");
            _ = mongoDatabase ?? throw new BackupFailure("BACKUP_MONGO_DATABASE is not set");
        }
        else
        {
            _ = postgresUri ?? throw new BackupFailure("BACKUP_PG_URI is not set");
        }

        var s3 = Value("BACKUP_S3_BUCKET") is { } bucket
            ? new S3Settings(bucket, Value("BACKUP_S3_ENDPOINT"), Value("BACKUP_S3_REGION"), Value("BACKUP_S3_PREFIX"),
                Value("BACKUP_S3_ACCESS_KEY_ID"), Value("BACKUP_S3_SECRET_ACCESS_KEY"))
            : null;

        var drive = Value("BACKUP_GDRIVE_REFRESH_TOKEN") is { } refreshToken
            ? new DriveSettings(
                Value("BACKUP_GDRIVE_CLIENT_ID") ?? throw new BackupFailure("BACKUP_GDRIVE_CLIENT_ID is required once BACKUP_GDRIVE_REFRESH_TOKEN is set"),
                Value("BACKUP_GDRIVE_CLIENT_SECRET") ?? throw new BackupFailure("BACKUP_GDRIVE_CLIENT_SECRET is required once BACKUP_GDRIVE_REFRESH_TOKEN is set"),
                refreshToken,
                Value("BACKUP_GDRIVE_FOLDER") ?? "pspad-backups")
            : null;

        return new BackupSettings(
            stack, time, timeZone, keepDays, Value("BACKUP_REMOTE_PRUNE") != "false", Value("BACKUP_AGE_RECIPIENT"),
            mongoUri, mongoDatabase, postgresUri, Value("BACKUP_SERVICE_PROBE"), Value("BACKUP_PING_URL"), s3, drive);
    }
}
