using PSPad.Contracts;

namespace PSPad.App.State;

public sealed record OutboxEntry(long Position, Guid CommandId, CommandEnvelope Envelope);
