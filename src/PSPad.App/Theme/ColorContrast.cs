using MudBlazor;
using MudBlazor.Utilities;

namespace PSPad.App.Theme;

public static class ColorContrast
{
    public static double Ratio(MudColor a, MudColor b)
    {
        var lumA = RelativeLuminance(a);
        var lumB = RelativeLuminance(b);
        var (lighter, darker) = lumA >= lumB ? (lumA, lumB) : (lumB, lumA);

        return (lighter + 0.05) / (darker + 0.05);
    }

    public static string ReadableOn(MudColor background) =>
        Ratio(background, Colors.Shades.White) >= Ratio(background, Colors.Gray.Darken4)
            ? Colors.Shades.White
            : Colors.Gray.Darken4;

    public static MudColor DeepenUntil(MudColor color, Func<MudColor, bool> readable) =>
        Step(color, readable, -0.01);

    public static MudColor LightenUntil(MudColor color, Func<MudColor, bool> readable) =>
        Step(color, readable, 0.01);

    static MudColor Step(MudColor color, Func<MudColor, bool> readable, double delta)
    {
        while (!readable(color) && (delta < 0 ? color.L > 0 : color.L < 1))
        {
            color = color.SetL(Math.Clamp(color.L + delta, 0, 1));
        }

        return color;
    }

    static double RelativeLuminance(MudColor color) =>
        0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);

    static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
