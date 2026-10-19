using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record SetUserLanguage(Guid CommandId, Guid UserId, string Language) : ICommand;
