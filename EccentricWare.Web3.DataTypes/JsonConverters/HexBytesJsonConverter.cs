using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for HexBytes that serializes as hex string with 0x prefix.
/// Handles variable-length byte arrays efficiently.
/// </summary>
public sealed class HexBytesJsonConverter : JsonConverter<HexBytes>
{
    public override HexBytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return HexBytes.Empty;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert {reader.TokenType} to HexBytes");

        var str = reader.GetString();
        if (str is null)
            return HexBytes.Empty;

        if (!HexBytes.TryParse(str, out var result))
            throw new JsonException("Invalid hex string for HexBytes");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, HexBytes value, JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteStringValue("0x");
            return;
        }

        // For small payloads, use stack allocation
        int requiredLength = value.HexLength + 2;
        if (requiredLength <= 512)
        {
            Span<byte> buffer = stackalloc byte[requiredLength];
            if (value.TryFormat(buffer, out int bytesWritten))
            {
                writer.WriteStringValue(buffer.Slice(0, bytesWritten));
                return;
            }
        }

        // Fallback for larger payloads
        writer.WriteStringValue(value.ToString());
    }
}

