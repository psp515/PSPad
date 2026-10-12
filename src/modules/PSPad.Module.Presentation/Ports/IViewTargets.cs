namespace PSPad.Module.Presentation;

/// <summary>
/// Answers whether a user may file a list or order an area, so a view is never saved for something the user cannot see.
/// Each check throws <see cref="PSPad.Abstractions.DomainRejectedException"/> when the user may not.
/// </summary>
public interface IViewTargets
{
    Task RequireListAsync(Guid userId, Guid listId, CancellationToken ct);

    Task RequireAreaAsync(Guid userId, Guid areaId, CancellationToken ct);
}
