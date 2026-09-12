namespace PSPad.Module.Tasks.Tasks;

public sealed record Step(Guid Id, string Name, DateOnly? DueOn, bool Checked, int Position);
