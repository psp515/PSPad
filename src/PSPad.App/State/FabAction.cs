namespace PSPad.App.State;

public sealed record FabAction(string Label, string Icon, Func<Task> Invoke);
