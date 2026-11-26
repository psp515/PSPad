namespace PSPad.Module.Reminders;

internal sealed class Tally
{
    public int Sent { get; set; }

    public int Skipped { get; set; }

    public int Retried { get; set; }

    public int Failed { get; set; }
}
