using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for uint256 that serializes as hex string.
/// </summary>
public sealed class UInt256JsonConverter : JsonConverter<uint256>
{
    public override uint256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (str is null)
                return uint256.Zero;

            // Try hex first, then decimal
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint256.Parse(str, CultureInfo.InvariantCulture);

            if (uint256.TryParseDecimal(str, out var result))
                return result;

            return uint256.Parse(str, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetUInt64(out var ulongValue))
                return new uint256(ulongValue);
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to uint256");
    }

    public override void Write(Utf8JsonWriter writer, uint256 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

