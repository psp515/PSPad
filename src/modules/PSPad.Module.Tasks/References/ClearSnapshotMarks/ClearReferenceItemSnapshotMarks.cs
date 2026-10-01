using PSPad.Abstractions;

namespace PSPad.Module.Tasks.References;

public sealed record ClearReferenceItemSnapshotMarks(Guid CommandId, Guid UserId, Guid ItemId) : ICommand;
