using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PSPad.Backup.Configuration;

namespace PSPad.Backup.Storage;

public sealed class S3Store(IAmazonS3 client, string bucket, string keyPrefix) : IBackupStore
{
    public string Name => "s3";

    public static S3Store Create(S3Settings settings, BackupStack stack)
    {
        var config = new AmazonS3Config
        {
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        if (settings.Endpoint is { } endpoint)
        {
            config.ServiceURL = endpoint;
            config.ForcePathStyle = true;
            config.AuthenticationRegion = settings.Region ?? "us-east-1";
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region ?? "us-east-1");
        }

        var client = settings.AccessKeyId is { } key && settings.SecretAccessKey is { } secret
            ? new AmazonS3Client(new BasicAWSCredentials(key, secret), config)
            : new AmazonS3Client(config);

        return new S3Store(client, settings.Bucket, settings.KeyPrefix(stack));
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct)
    {
        var names = new List<string>();
        var request = new ListObjectsV2Request { BucketName = bucket, Prefix = keyPrefix };
        ListObjectsV2Response response;
        do
        {
            response = await client.ListObjectsV2Async(request, ct);
            names.AddRange((response.S3Objects ?? [])
                .Select(entry => entry.Key[keyPrefix.Length..])
                .Where(name => !name.Contains('/')));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        return names;
    }

    public Task PutAsync(string source, string fileName, CancellationToken ct) =>
        client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = keyPrefix + fileName, FilePath = source }, ct);

    public async Task FetchAsync(string fileName, string destination, CancellationToken ct)
    {
        using var response = await client.GetObjectAsync(bucket, keyPrefix + fileName, ct);
        await response.WriteResponseStreamToFileAsync(destination, append: false, ct);
    }

    public Task DeleteAsync(string fileName, CancellationToken ct) =>
        client.DeleteObjectAsync(bucket, keyPrefix + fileName, ct);
}
