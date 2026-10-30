using PSPad.Abstractions;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public static class InvestmentLinkPlan
{
    public static IReadOnlyList<ICommand> Commands(
        Guid userId, Guid budgetId, YearMonth month, bool monthExists, IReadOnlyList<LinkChoice> ticked, Func<Guid> newId)
    {
        var worth = ticked.Where(choice => choice.Value.Value > 0).ToList();
        var commands = new List<ICommand>();

        if (!monthExists && worth.Count > 0)
        {
            commands.Add(new StartBalanceMonth(newId(), userId, budgetId, month));
        }

        foreach (var choice in worth)
        {
            var money = new Money(choice.Value.Value, choice.Account.Currency, choice.RateToPln, choice.RateDate);
            commands.Add(choice.Existing is { } existing
                ? new EditHolding(newId(), userId, budgetId, month, existing.HoldingId, existing.Name, existing.Type,
                    money, choice.Account.Id)
                : new AddHolding(newId(), userId, budgetId, month, newId(), choice.Account.Name, HoldingType.Investment,
                    money, choice.Account.Id));
        }

        return commands;
    }
}
