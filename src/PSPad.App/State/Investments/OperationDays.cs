using PSPad.Module.Investments.Operations;

namespace PSPad.App.State.Investments;

public static class OperationDays
{
    public static IReadOnlyList<OperationDay> Of(IEnumerable<InvestmentOperation> operations, TimeZoneInfo zone) =>
    [
        .. operations
            .OrderByDescending(operation => operation.At)
            .ThenByDescending(operation => operation.Id)
            .GroupBy(operation => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(operation.At, zone).DateTime))
            .Select(day => new OperationDay(day.Key, [.. day]))
    ];
}
