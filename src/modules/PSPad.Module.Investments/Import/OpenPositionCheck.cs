using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;

namespace PSPad.Module.Investments.Import;

public static class OpenPositionCheck
{
    public static IReadOnlyList<OpenPositionDifference> Differences(
        IReadOnlyList<XtbOpenPosition> positions, IEnumerable<InvestmentOperation> operations)
    {
        var open = Lots.Of(operations).Open.GroupBy(lot => lot.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (group.First().Ticker, Volume: group.Sum(lot => lot.Quantity)),
                StringComparer.Ordinal);
        var inFile = positions.GroupBy(position => position.PositionId, StringComparer.Ordinal)
            .Select(group => (PositionId: group.Key, group.First().Ticker, Volume: group.Sum(position => position.Volume)))
            .ToList();
        var fileIds = inFile.Select(position => position.PositionId).ToHashSet(StringComparer.Ordinal);

        var differences = inFile
            .Select(position => new OpenPositionDifference(position.PositionId, position.Ticker, position.Volume,
                open.TryGetValue(position.PositionId, out var lot) ? lot.Volume : 0m))
            .Where(difference => difference.FileVolume != difference.ImportedVolume);
        var unknown = open.Where(lot => !fileIds.Contains(lot.Key))
            .Select(lot => new OpenPositionDifference(lot.Key, lot.Value.Ticker, 0m, lot.Value.Volume));
        return differences.Concat(unknown).ToList();
    }
}
