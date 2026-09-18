using PSPad.Contracts;

namespace PSPad.App.State.Outbox;

public sealed record OutboxEntry(long Position, Guid CommandId, CommandEnvelope Envelope);
