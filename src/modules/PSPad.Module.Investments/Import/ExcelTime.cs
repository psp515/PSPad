namespace PSPad.Module.Investments.Import;

public static class ExcelTime
{
    static readonly DateTime Epoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc);

    public static DateTimeOffset ToUtc(double serial) =>
        new(Epoch.AddSeconds(Math.Round(serial * 86_400, MidpointRounding.AwayFromZero)), TimeSpan.Zero);
}
