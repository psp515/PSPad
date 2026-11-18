using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class WebPushSender(
    HttpClient httpClient, VapidKeyring keyring, IOptions<ReminderOptions> options, ILogger<WebPushSender> logger)
    : IPushSender
{
    readonly HttpClient _http = WithNormalUrgency(httpClient);

    public async Task<int?> SendAsync(PushSubscriptionRecord to, PushNote note, TimeSpan ttl, CancellationToken ct)
    {
        using var authentication = Authentication(await keyring.GetAsync(ct));
        var client = new PushServiceClient(_http)
        {
            AutoRetryAfter = false,
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
            DefaultAuthentication = authentication,
        };
        var payload = JsonSerializer.Serialize(new { title = note.Title, body = note.Body, url = note.Url, tag = note.Tag });
        var message = new PushMessage(payload)
        {
            TimeToLive = (int)ttl.TotalSeconds,
            Topic = note.Tag.Replace("-", ""),
            Urgency = PushMessageUrgency.Normal,
        };
        var subscription = new PushSubscription { Endpoint = to.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, to.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, to.Auth);

        try
        {
            await client.RequestPushMessageDeliveryAsync(subscription, message, ct);
            return 201;
        }
        catch (PushServiceClientException e)
        {
            return (int)e.StatusCode;
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "Push service unreachable");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Push service timed out");
            return null;
        }
    }

    // The library leaves Urgency out for Normal, the RFC 8030 default; the spec sends it explicitly.
    static HttpClient WithNormalUrgency(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("Urgency", "normal");
        return client;
    }

    VapidAuthentication Authentication(VapidKeys keys)
    {
        var authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey);
        var subject = string.IsNullOrWhiteSpace(options.Value.Contact)
            ? options.Value.AppBaseAddress
            : options.Value.Contact;
        // The library refuses a subject that is neither mailto: nor https:, as a localhost http address is.
        if (subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            subject.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            authentication.Subject = subject;
        }

        return authentication;
    }
}
