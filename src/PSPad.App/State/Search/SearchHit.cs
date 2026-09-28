namespace PSPad.App.State.Search;

public sealed record SearchHit(Guid Id, string Name, string Path, bool IsList, Guid? ListId = null);
