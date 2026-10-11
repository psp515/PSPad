using System.Text.Json;
using System.Text.Json.Serialization;

namespace PSPad.Module.Money.Values;

public sealed class YearMonthJsonConverter : JsonConverter<YearMonth>
{
    public override YearMonth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A month is a yyyy-MM string.");
        }

        try
        {
            return YearMonth.Parse(reader.GetString()!);
        }
        catch (FormatException invalid)
        {
            throw new JsonException(invalid.Message, invalid);
        }
    }

    public override void Write(Utf8JsonWriter writer, YearMonth value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
