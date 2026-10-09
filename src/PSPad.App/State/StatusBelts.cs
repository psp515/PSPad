namespace PSPad.App.State;

public sealed class StatusBelts
{
    readonly List<Belt> _visible = [];
    readonly HashSet<BeltKind> _dismissed = [];

    public IReadOnlyList<Belt> Visible => _visible;

    public event Action? Changed;

    public void Show(BeltKind kind, string text, string? actionText = null, Func<Task>? action = null)
    {
        if (_dismissed.Contains(kind))
        {
            return;
        }

        _visible.RemoveAll(belt => belt.Kind == kind);
        _visible.Insert(0, new Belt(kind, text, actionText, action));
        Changed?.Invoke();
    }

    public void Dismiss(BeltKind kind)
    {
        if (_visible.RemoveAll(belt => belt.Kind == kind) == 0 && _dismissed.Contains(kind))
        {
            return;
        }

        _dismissed.Add(kind);
        Changed?.Invoke();
    }

    public void Clear(BeltKind kind)
    {
        var hidden = _visible.RemoveAll(belt => belt.Kind == kind) > 0;
        var forgotten = _dismissed.Remove(kind);

        if (hidden || forgotten)
        {
            Changed?.Invoke();
        }
    }
}
