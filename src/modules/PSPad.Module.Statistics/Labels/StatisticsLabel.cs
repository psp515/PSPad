namespace PSPad.Module.Statistics;

public sealed record StatisticsLabel
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public LabelKind Kind { get; init; }
    public string Name { get; init; } = "";
    public bool Deleted { get; init; }
}
