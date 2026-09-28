using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State.Search;

public sealed record SearchHit(
    Guid Id, string Name, string Path, SearchHitKind Kind, string Href, ListKind? ListKind = null);
