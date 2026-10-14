namespace PSPad.Module.Money.Import;

public sealed record RateBook(
    IReadOnlyDictionary<RateRequest, ImportRate> Known, IReadOnlyDictionary<RateRequest, string> Failed)
{
    public static RateBook Empty { get; } =
        new(new Dictionary<RateRequest, ImportRate>(), new Dictionary<RateRequest, string>());
}
