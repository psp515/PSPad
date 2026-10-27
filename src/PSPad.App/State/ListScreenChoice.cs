using PSPad.Module.Tasks.Lists;

namespace PSPad.App.State;

public sealed record ListScreenChoice(ListFilter Filter, ListSort Sort, ListScreenView View)
{
    public static ListScreenChoice Default { get; } = new(ListFilter.None, ListSort.Default, ListScreenView.List);

    public bool IsDefault => Filter.IsEmpty && Sort == ListSort.Default && View == ListScreenView.List;
}
