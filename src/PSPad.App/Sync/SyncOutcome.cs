namespace PSPad.App.Sync;

public sealed record SyncOutcome(int Pushed, int Pulled, IReadOnlyList<string> Rejections);
