using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;

namespace PSPad.Api.Tests.Commands;

public sealed class StaleAreaStore(IDocumentStore<Area> inner, StaleReads reads) : IDocumentStore<Area>
{
    public async Task<Area?> LoadAsync(Guid id, CancellationToken ct) =>
        reads.Next() ?? await inner.LoadAsync(id, ct);

    public Task<IReadOnlyList<Area>> LoadAllAsync(Guid userId, CancellationToken ct) =>
        inner.LoadAllAsync(userId, ct);
}
