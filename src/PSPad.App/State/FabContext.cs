namespace PSPad.App.State;

public sealed class FabContext
{
    public FabAction? Primary { get; private set; }

    public IReadOnlyList<FabAction> Secondary { get; private set; } = [];

    public event Action? Changed;

    public void Set(FabAction primary, params FabAction[] secondary)
    {
        Primary = primary;
        Secondary = secondary;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Primary = null;
        Secondary = [];
        Changed?.Invoke();
    }
}
