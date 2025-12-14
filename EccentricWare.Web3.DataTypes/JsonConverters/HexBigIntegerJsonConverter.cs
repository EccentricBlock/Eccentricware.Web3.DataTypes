using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for HexBigInteger that serializes as hex string.
/// </summary>
public sealed class HexBigIntegerJsonConverter : JsonConverter<HexBigInteger>
{
    public override HexBigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (str is null)
                return HexBigInteger.Zero;

            // Try hex first, then decimal
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
                return HexBigInteger.Parse(str, CultureInfo.InvariantCulture);

            if (HexBigInteger.TryParseDecimal(str, out var result))
                return result;

            return HexBigInteger.Parse(str, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var longValue))
                return new HexBigInteger(longValue);
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to HexBigInteger");
    }

    public override void Write(Utf8JsonWriter writer, HexBigInteger value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

