namespace PSPad.App.Push;

public sealed record PushDeviceSubscription(string Endpoint, string P256dh, string Auth);
