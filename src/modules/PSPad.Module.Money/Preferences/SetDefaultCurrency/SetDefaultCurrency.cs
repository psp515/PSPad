using PSPad.Abstractions;

namespace PSPad.Module.Money.Preferences;

public sealed record SetDefaultCurrency(Guid CommandId, Guid UserId, string Currency) : ICommand;
