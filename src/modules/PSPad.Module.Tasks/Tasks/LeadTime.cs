using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record LeadTime(int Amount, LeadUnit Unit)
{
    public static LeadTime Of(int amount, LeadUnit unit)
    {
        var lead = new LeadTime(amount, unit);
        lead.Validate();
        return lead;
    }

    public void Validate()
    {
        if (Amount is < 1 or > 99)
        {
            throw new DomainRejectedException("A lead time must be between 1 and 99.");
        }

        if (!Enum.IsDefined(Unit))
        {
            throw new DomainRejectedException("A lead time is counted in days, weeks or months.");
        }
    }

    public DateOnly FirstShownFor(DateOnly day) => Unit switch
    {
        LeadUnit.Weeks => day.AddDays(-7 * Amount),
        LeadUnit.Months => day.AddMonths(-Amount),
        _ => day.AddDays(-Amount)
    };

    // A month back from a later month-end can clamp onto today, so months reach one month further.
    public DateOnly ReachFrom(DateOnly today) => Unit switch
    {
        LeadUnit.Weeks => today.AddDays(7 * Amount),
        LeadUnit.Months => today.AddMonths(Amount + 1),
        _ => today.AddDays(Amount)
    };

    public bool Shows(DateOnly day, DateOnly today) => day > today && FirstShownFor(day) <= today;
}
