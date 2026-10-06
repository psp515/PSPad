using PSPad.App.Sync;
using PSPad.Contracts;

namespace PSPad.App.Api;

public interface ISyncApi
{
    Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes);

    Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full);

    Task<JoinOutcome> JoinAsync(string token, string code);
}
