namespace PSPad.Api.Reminders;

public sealed class PushServiceHosts
{
    sealed record PushService(string Host, string UsedBy);

    // A host with a leading dot covers its subdomains only.
    static readonly PushService[] BuiltIn =
    [
        new("fcm.googleapis.com", "Chrome, Edge outside Windows, Android, Opera, Samsung Internet"),
        new("android.googleapis.com", "Chrome subscriptions from its older GCM endpoint"),
        new(".push.services.mozilla.com", "Firefox"),
        new(".push.apple.com", "Safari on macOS, iPhone and iPad"),
        new(".notify.windows.com", "Edge on Windows"),
    ];

    readonly string[] _hosts;

    public PushServiceHosts(string extra) =>
        _hosts =
        [
            .. BuiltIn.Select(service => service.Host),
            .. extra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ];

    public bool IsKnown(Uri endpoint) =>
        endpoint.Scheme == Uri.UriSchemeHttps
        && endpoint.IsDefaultPort
        && _hosts.Any(host => Matches(endpoint.IdnHost, host));

    static bool Matches(string host, string allowed) =>
        allowed.StartsWith('.')
            ? host.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)
            : host.Equals(allowed, StringComparison.OrdinalIgnoreCase);
}
