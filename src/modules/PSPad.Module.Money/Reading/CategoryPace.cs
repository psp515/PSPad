namespace PSPad.Module.Money.Reading;

public sealed record CategoryPace(string Category, decimal Spent, decimal? UsualByNow, decimal? UsualMonth)
{
    public decimal? Delta => Spent - UsualByNow;
}
