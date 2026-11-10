using PSPad.Abstractions;

namespace PSPad.Module.Presentation.UserViews;

public sealed record ShowFinishedOnSchedule(Guid CommandId, Guid UserId, bool Show) : ICommand;
