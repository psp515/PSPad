using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskTime(TimeOnly Start, TimeOnly? End)
{
    public static TaskTime Of(TimeOnly start, TimeOnly? end)
    {
        var time = new TaskTime(start, end);
        time.Validate();
        return time;
    }

    public void Validate()
    {
        if (End is { } end && end <= Start)
        {
            throw new DomainRejectedException("A task must end after it starts.");
        }
    }
}
