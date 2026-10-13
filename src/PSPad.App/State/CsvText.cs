using System.Text;

namespace PSPad.App.State;

public static class CsvText
{
    static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string Decode(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(1250)!.GetString(bytes);
        }
    }
}
