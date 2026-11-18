namespace PSPad.Contracts;

public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth, string DeviceLabel);
