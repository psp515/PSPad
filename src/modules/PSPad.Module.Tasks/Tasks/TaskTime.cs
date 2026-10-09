using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskTime(TimeOnly Start, TimeOnly? End)
{
    [JsonIgnore]
    public bool Overnight => End < Start;

    public static TaskTime Of(TimeOnly start, TimeOnly? end)
    {
        var time = new TaskTime(start, end);
        time.Validate();
        return time;
    }

    public void Validate()
    {
        if (End == Start)
        {
            throw new DomainRejectedException("A task can't end when it starts.");
        }
    }
}
