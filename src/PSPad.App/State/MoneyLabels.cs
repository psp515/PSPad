using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;

namespace PSPad.App.State;

public static class MoneyLabels
{
    public static string Of(this IStringLocalizer<BudgetsStrings> text, HoldingType type) => type switch
    {
        HoldingType.Cash => text["HoldingCash"],
        HoldingType.Savings => text["HoldingSavings"],
        _ => text["HoldingInvestment"]
    };

    public static string Of(this IStringLocalizer<BudgetsStrings> text, CategoryKind kind) =>
        kind == CategoryKind.Expense ? text["KindExpense"] : text["KindIncome"];
}
