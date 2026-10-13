namespace PSPad.App.State;

public static class ImportProgress
{
    const int Every = 25;

    public static bool IsDue(int done, int total) => done % Every == 0 || done >= total;
}
