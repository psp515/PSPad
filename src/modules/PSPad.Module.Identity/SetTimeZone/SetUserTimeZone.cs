using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record SetUserTimeZone(Guid CommandId, Guid UserId, string TimeZone) : ICommand;
