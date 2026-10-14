using System.Runtime.CompilerServices;
using System.Text;

namespace PSPad.App.Live;

public static class SseReader
{
    public const string Heartbeat = "heartbeat";

    const string EventField = "event:";

    public static async IAsyncEnumerable<string> FramesAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string? name = null;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                if (name is not null)
                {
                    yield return name;
                    name = null;
                }

                continue;
            }

            if (line[0] == ':')
            {
                yield return Heartbeat;
            }
            else if (line.StartsWith(EventField, StringComparison.Ordinal))
            {
                name = line[EventField.Length..].Trim();
            }
        }
    }
}
