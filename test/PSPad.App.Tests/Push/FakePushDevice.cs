using Microsoft.JSInterop;
using PSPad.App.Push;

namespace PSPad.App.Tests.Push;

public sealed class FakePushDevice : IPushDevice
{
    public PushSupport Support { get; set; } = new(true, false, "granted", "https://push.example/a", "Linux");

    public PushDeviceSubscription? Current { get; set; } = new("https://push.example/a", "p256dh", "auth");

    public bool Throws { get; set; }

    public Task<PushSupport> SupportAsync() => Throws ? throw new JSException("no push") : Task.FromResult(Support);

    public Task<PushDeviceSubscription?> CurrentAsync() =>
        Throws ? throw new JSException("no push") : Task.FromResult(Current);

    public Task<PushDeviceSubscription?> SubscribeAsync(string publicKey) => Task.FromResult(Current);

    public Task<string?> UnsubscribeAsync() => Task.FromResult(Current?.Endpoint);
}
