namespace PSPad.App.Push;

public sealed record PushSupport(bool Supported, bool NeedsHomeScreen, string Permission, string? Endpoint, string Label);
