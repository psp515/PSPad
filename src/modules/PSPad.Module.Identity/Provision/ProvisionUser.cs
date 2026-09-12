using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record ProvisionUser(
    Guid CommandId, Guid UserId, string Subject, string DisplayName, string TimeZone) : ICommand;
