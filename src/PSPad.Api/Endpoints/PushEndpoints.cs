using System.Buffers.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Api.Reminders;
using PSPad.Contracts;
using PSPad.Module.Identity;
using PSPad.Module.Reminders;

namespace PSPad.Api.Endpoints;

public static class PushEndpoints
{
    const int MaxEndpointLength = 2048;
    const int MaxKeyLength = 200;
    const int PublicKeyLength = 65;
    const byte UncompressedPoint = 0x04;
    const int AuthLength = 16;
    const int MaxLabelLength = 100;
    static readonly TimeSpan TestTimeToLive = TimeSpan.FromSeconds(60);

    public static void MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("push/key", async (VapidKeyring keyring, CancellationToken ct) =>
            Results.Ok(new PushKeyResponse((await keyring.GetAsync(ct)).PublicKey)));

        app.MapPut("push/subscriptions", async (
            PushSubscriptionRequest request,
            IPushSubscriptionStore store,
            ICurrentUser current,
            IClock clock,
            CancellationToken ct) =>
        {
            if (!IsAcceptableEndpoint(request.Endpoint) || !IsPublicKey(request.P256dh) || !IsAuth(request.Auth))
            {
                return Results.BadRequest();
            }

            var existing = await store.FindAsync(request.Endpoint, ct);
            if (existing is not null && existing.UserId != current.UserId)
            {
                existing = null;
            }

            var label = (request.DeviceLabel ?? "").Trim();
            await store.SaveAsync(new PushSubscriptionRecord(
                PushSubscriptionRecord.IdFor(request.Endpoint),
                current.UserId,
                request.Endpoint,
                request.P256dh,
                request.Auth,
                label.Length > MaxLabelLength ? label[..MaxLabelLength] : label,
                existing?.CreatedAt ?? clock.UtcNow,
                existing?.LastSuccessAt), ct);
            return Results.NoContent();
        });

        app.MapMethods("push/subscriptions", ["DELETE"], async (
            [FromBody] PushEndpointRequest request,
            IPushSubscriptionStore store,
            ICurrentUser current,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.Endpoint))
            {
                return Results.BadRequest();
            }

            var existing = await store.FindAsync(request.Endpoint, ct);
            if (existing is null)
            {
                return Results.NoContent();
            }

            if (existing.UserId != current.UserId)
            {
                return Results.NotFound();
            }

            await store.DeleteAsync(request.Endpoint, ct);
            return Results.NoContent();
        });

        app.MapPost("push/test", async (
            PushEndpointRequest request,
            IPushSubscriptionStore store,
            IPushSender sender,
            IDocumentStore<User> users,
            IOptions<ReminderOptions> options,
            ICurrentUser current,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.Endpoint))
            {
                return Results.BadRequest();
            }

            var subscription = await store.FindAsync(request.Endpoint, ct);
            if (subscription is null || subscription.UserId != current.UserId)
            {
                return Results.NotFound();
            }

            var user = await users.LoadAsync(current.UserId, ct);
            var note = ReminderTexts.Test(user?.Language ?? SupportedLanguages.Default, options.Value.AppBaseAddress);
            switch (await TestOutcomeAsync(sender, subscription, note, ct))
            {
                case PushOutcome.Delivered:
                    return Results.NoContent();
                case PushOutcome.Gone:
                    await store.DeleteAsync(subscription.Endpoint, ct);
                    return Results.StatusCode(StatusCodes.Status410Gone);
                default:
                    return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });
    }

    static bool IsAcceptableEndpoint(string? endpoint) =>
        endpoint is { Length: > 0 and <= MaxEndpointLength }
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.HostNameType == UriHostNameType.Dns;

    static async Task<PushOutcome> TestOutcomeAsync(
        IPushSender sender, PushSubscriptionRecord subscription, PushNote note, CancellationToken ct)
    {
        try
        {
            return PushAnswer.Of(await sender.SendAsync(subscription, note, TestTimeToLive, ct));
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return PushOutcome.Failed;
        }
    }

    static bool IsPublicKey(string? value) =>
        Decoded(value) is { Length: PublicKeyLength } bytes && bytes[0] == UncompressedPoint;

    static bool IsAuth(string? value) => Decoded(value) is { Length: AuthLength };

    static byte[]? Decoded(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxKeyLength
            || !value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return null;
        }

        try
        {
            return Base64Url.DecodeFromChars(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
