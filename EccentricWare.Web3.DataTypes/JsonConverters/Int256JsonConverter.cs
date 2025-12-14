using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for int256 that serializes as hex string with 0x prefix.
/// Supports negative values with leading '-'.
/// </summary>
public sealed class Int256JsonConverter : JsonConverter<int256>
{
    public override int256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (str is null)
                return int256.Zero;

            // Try hex first, then decimal
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
                return int256.Parse(str, CultureInfo.InvariantCulture);

            if (int256.TryParseDecimal(str, out var result))
                return result;

            return int256.Parse(str, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var longValue))
                return new int256(longValue);
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to int256");
    }

    public override void Write(Utf8JsonWriter writer, int256 value, JsonSerializerOptions options)
    {
        // Write directly to UTF-8 without string allocation
        Span<byte> buffer = stackalloc byte[69]; // "-0x" + 64 hex chars
        if (value.TryFormat(buffer, out int bytesWritten, default, CultureInfo.InvariantCulture))
        {
            writer.WriteStringValue(buffer.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

