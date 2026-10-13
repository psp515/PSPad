using PSPad.Abstractions;

namespace PSPad.Module.Money.Import;

public sealed record PlannedCommand(ICommand Command, int? Line);
