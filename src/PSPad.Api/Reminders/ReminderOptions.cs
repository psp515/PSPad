namespace PSPad.Api.Reminders;

public sealed class ReminderOptions
{
    public const string Section = "Reminders";

    public string AppBaseAddress { get; set; } = "http://localhost:5001";

    public string Contact { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public List<string> WebPushHosts { get; set; } = [];

    public List<string> AdditionalWebPushHosts { get; set; } = [];
}
