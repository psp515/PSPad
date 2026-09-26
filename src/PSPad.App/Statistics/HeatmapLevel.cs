namespace PSPad.App.Statistics;

public static class HeatmapLevel
{
    public const int Levels = 5;

    public static int Of(int count, int maximum)
    {
        if (count <= 0 || maximum <= 0)
        {
            return 0;
        }

        var ratio = (double)count / maximum;

        return ratio switch
        {
            <= 0.25 => 1,
            <= 0.5 => 2,
            <= 0.75 => 3,
            _ => 4
        };
    }
}
