namespace PSPad.Backup.Configuration;

public sealed record DriveSettings(string ClientId, string ClientSecret, string RefreshToken, string Folder);
