namespace PSPad.Api.Reminders;

public sealed class PushServiceHosts(IEnumerable<string> hosts)
{
    readonly string[] _hosts = [.. hosts.Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim())];

    public bool IsKnown(Uri endpoint) =>
        endpoint.Scheme == Uri.UriSchemeHttps
        && endpoint.IsDefaultPort
        && _hosts.Any(host => Matches(endpoint.IdnHost, host));

    // A host with a leading dot covers its subdomains only.
    static bool Matches(string host, string allowed) =>
        allowed.StartsWith('.')
            ? host.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)
            : host.Equals(allowed, StringComparison.OrdinalIgnoreCase);
}
