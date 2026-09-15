using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record SetUserDisplayName(Guid CommandId, Guid UserId, string DisplayName) : ICommand;
