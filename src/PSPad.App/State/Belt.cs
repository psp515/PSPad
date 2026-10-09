namespace PSPad.App.State;

public sealed record Belt(BeltKind Kind, string Text, string? ActionText = null, Func<Task>? Action = null);
