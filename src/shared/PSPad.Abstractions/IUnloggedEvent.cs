namespace PSPad.Abstractions;

/// <summary>
/// An event that changes its aggregate's document but writes no row to the event log,
/// records no command id and is not dispatched to projections.
/// </summary>
public interface IUnloggedEvent;
