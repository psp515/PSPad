namespace PSPad.Backup.Configuration;

public sealed record S3Settings(
    string Bucket, string? Endpoint, string? Region, string? Prefix, string? AccessKeyId, string? SecretAccessKey)
{
    public string KeyPrefix(BackupStack stack)
    {
        var prefix = Prefix?.Trim('/');
        var folder = stack.ToString().ToLowerInvariant();
        return string.IsNullOrEmpty(prefix) ? $"{folder}/" : $"{prefix}/{folder}/";
    }
}
