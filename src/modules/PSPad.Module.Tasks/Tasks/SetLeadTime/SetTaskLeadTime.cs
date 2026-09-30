using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskLeadTime(Guid CommandId, Guid UserId, Guid TaskId, LeadTime? LeadTime)
    : ICommand;
