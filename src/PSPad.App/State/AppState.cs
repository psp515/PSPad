namespace PSPad.App.State;

public sealed class AppState
{
    public Guid UserId { get; set; }

    public string TimeZone { get; set; } = "Etc/UTC";

    public DateOnly Today { get; set; }
}
