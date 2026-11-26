namespace PSPad.App.Push;

public interface IPushDevice
{
    Task<PushSupport> SupportAsync();

    Task<PushDeviceSubscription?> CurrentAsync();

    Task<PushDeviceSubscription?> SubscribeAsync(string publicKey);

    Task<string?> UnsubscribeAsync();
}
