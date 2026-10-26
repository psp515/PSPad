namespace PSPad.App.State;

public sealed record StepDraft(string Name, DateOnly? DueOn = null);
