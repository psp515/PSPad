using System.Security.Cryptography;
using System.Text;

namespace PSPad.Module.Reminders;

public sealed record PushSubscriptionRecord(
    string Id,
    Guid UserId,
    string Endpoint,
    string P256dh,
    string Auth,
    string DeviceLabel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSuccessAt)
{
    public static string IdFor(string endpoint) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
}
