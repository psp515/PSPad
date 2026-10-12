using PSPad.Abstractions;

namespace PSPad.Module.Presentation.Tests.Fakes;

public sealed class FakeViewTargets : IViewTargets
{
    readonly HashSet<(Guid User, Guid List)> _lists = [];
    readonly HashSet<(Guid User, Guid Area)> _areas = [];

    public FakeViewTargets SeesList(Guid userId, Guid listId)
    {
        _lists.Add((userId, listId));
        return this;
    }

    public FakeViewTargets OwnsArea(Guid userId, Guid areaId)
    {
        _areas.Add((userId, areaId));
        return this;
    }

    public Task RequireListAsync(Guid userId, Guid listId, CancellationToken ct) =>
        _lists.Contains((userId, listId))
            ? Task.CompletedTask
            : throw new DomainRejectedException("That list belongs to somebody else.");

    public Task RequireAreaAsync(Guid userId, Guid areaId, CancellationToken ct) =>
        _areas.Contains((userId, areaId))
            ? Task.CompletedTask
            : throw new DomainRejectedException("That area belongs to somebody else.");
}
