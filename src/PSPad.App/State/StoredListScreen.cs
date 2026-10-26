namespace PSPad.App.State;

sealed record StoredListScreen(
    string[]? When, string[]? Priority, bool Starred, bool HasGoal, Guid[]? Goals,
    string? Order, bool Descending, string? View);
