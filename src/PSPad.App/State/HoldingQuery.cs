namespace PSPad.App.State;

public static class HoldingQuery
{
    const string NewHolding = "new";

    public static (Guid SnapshotId, Guid? HoldingId)? From(string uri)
    {
        if (!Guid.TryParse(QueryString.Value(uri, "balance"), out var snapshotId))
        {
            return null;
        }

        var holding = QueryString.Value(uri, "holding");

        if (holding == NewHolding)
        {
            return (snapshotId, null);
        }

        return Guid.TryParse(holding, out var holdingId) ? (snapshotId, holdingId) : null;
    }

    public static string For(string uri, Guid snapshotId, Guid holdingId) =>
        $"{QueryString.Without(uri)}?balance={snapshotId}&holding={holdingId}";

    public static string ForNew(string uri, Guid snapshotId) =>
        $"{QueryString.Without(uri)}?balance={snapshotId}&holding={NewHolding}";
}
