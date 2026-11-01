using System.Globalization;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

internal static class LiveOperations
{
    public static IReadOnlyList<InvestmentOperation> Of(IEnumerable<InvestmentOperation> operations) =>
        operations.Where(operation => !operation.Deleted).OrderBy(operation => operation.At)
            .ThenBy(operation => BrokerOrder(operation) is null)
            .ThenBy(BrokerOrder)
            .ThenBy(operation => operation.Id).ToList();

    static long? BrokerOrder(InvestmentOperation operation) =>
        long.TryParse(operation.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static IReadOnlyList<InvestmentOperation> Until(
        IEnumerable<InvestmentOperation> operations, DateOnly day, TimeZoneInfo zone) =>
        Of(operations).Where(operation => DayOf(operation, zone) <= day).ToList();

    public static DateOnly DayOf(InvestmentOperation operation, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(operation.At, zone).DateTime);

    public static decimal Sum(IEnumerable<InvestmentOperation> operations, OperationKind kind, Func<InvestmentOperation, decimal> amount) =>
        operations.Where(operation => operation.Kind == kind).Sum(amount);
}
