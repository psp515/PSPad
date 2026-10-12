using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using PSPad.Backup.Configuration;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace PSPad.Backup.Storage;

public sealed class DriveStore(DriveService drive, string folder, string stack) : IBackupStore
{
    const string FolderType = "application/vnd.google-apps.folder";
    string? _folderId;

    public string Name => "gdrive";

    public static DriveStore Create(DriveSettings settings, BackupStack stack)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = settings.ClientId, ClientSecret = settings.ClientSecret },
            Scopes = [DriveService.Scope.DriveFile]
        });
        var credential = new UserCredential(flow, "pspad-backup", new TokenResponse { RefreshToken = settings.RefreshToken });
        var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PSPad backup"
        });

        return new DriveStore(drive, settings.Folder, stack.ToString().ToLowerInvariant());
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct) =>
        (await FilesInAsync(await FolderIdAsync(ct), ct)).Select(file => file.Name).ToArray();

    public async Task PutAsync(string source, string fileName, CancellationToken ct)
    {
        await using var stream = File.OpenRead(source);
        var upload = drive.Files.Create(
            new DriveFile { Name = fileName, Parents = [await FolderIdAsync(ct)] }, stream, "application/octet-stream");
        var progress = await upload.UploadAsync(ct);
        if (progress.Status != UploadStatus.Completed)
        {
            throw new BackupFailure($"Google Drive upload of {fileName} failed: {progress.Exception?.Message}");
        }
    }

    public async Task FetchAsync(string fileName, string destination, CancellationToken ct)
    {
        await using var stream = File.Create(destination);
        var progress = await drive.Files.Get(await FileIdAsync(fileName, ct)).DownloadAsync(stream, ct);
        if (progress.Status != DownloadStatus.Completed)
        {
            throw new BackupFailure($"Google Drive download of {fileName} failed: {progress.Exception?.Message}");
        }
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct) =>
        await drive.Files.Delete(await FileIdAsync(fileName, ct)).ExecuteAsync(ct);

    async Task<string> FileIdAsync(string fileName, CancellationToken ct) =>
        (await FilesInAsync(await FolderIdAsync(ct), ct)).FirstOrDefault(file => file.Name == fileName)?.Id
        ?? throw new BackupFailure($"no backup {fileName} on gdrive");

    async Task<string> FolderIdAsync(CancellationToken ct) =>
        _folderId ??= await FolderAsync(stack, await FolderAsync(folder, "root", ct), ct);

    async Task<string> FolderAsync(string name, string parent, CancellationToken ct)
    {
        var existing = (await FilesInAsync(parent, ct)).FirstOrDefault(file => file.Name == name && file.MimeType == FolderType);
        if (existing is not null)
        {
            return existing.Id;
        }

        var create = drive.Files.Create(new DriveFile { Name = name, MimeType = FolderType, Parents = [parent] });
        create.Fields = "id";
        return (await create.ExecuteAsync(ct)).Id;
    }

    async Task<IReadOnlyList<DriveFile>> FilesInAsync(string parent, CancellationToken ct)
    {
        var files = new List<DriveFile>();
        string? page = null;
        do
        {
            var list = drive.Files.List();
            list.Q = $"'{parent}' in parents and trashed = false";
            list.Fields = "nextPageToken, files(id, name, mimeType)";
            list.PageToken = page;
            var result = await list.ExecuteAsync(ct);
            files.AddRange(result.Files ?? []);
            page = result.NextPageToken;
        }
        while (page is not null);

        return files;
    }
}
