using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for Hash32 that serializes as hex string with 0x prefix.
/// Optimized for minimal allocations using UTF-8 spans.
/// </summary>
public sealed class Hash32JsonConverter : JsonConverter<Hash32>
{
    public override Hash32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert {reader.TokenType} to Hash32");

        // Fast path: try to use the raw UTF-8 span directly
        if (reader.HasValueSequence)
        {
            // Fallback for multi-segment sequences
            var str = reader.GetString();
            return str is null ? Hash32.Zero : Hash32.Parse(str);
        }

        ReadOnlySpan<byte> utf8 = reader.ValueSpan;
        
        // Handle 0x prefix
        if (utf8.Length >= 2 && utf8[0] == '0' && (utf8[1] == 'x' || utf8[1] == 'X'))
            utf8 = utf8.Slice(2);

        if (utf8.Length != Hash32.HexLength)
            throw new JsonException($"Hash32 requires exactly {Hash32.HexLength} hex characters");

        // Parse UTF-8 hex directly without string allocation
        Span<char> chars = stackalloc char[Hash32.HexLength];
        for (int i = 0; i < Hash32.HexLength; i++)
            chars[i] = (char)utf8[i];

        if (!Hash32.TryParse(chars, out var result))
            throw new JsonException("Invalid hex string for Hash32");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Hash32 value, JsonSerializerOptions options)
    {
        // Write directly to UTF-8 buffer without string allocation
        Span<byte> buffer = stackalloc byte[66]; // "0x" + 64 hex chars
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            writer.WriteStringValue(buffer.Slice(0, bytesWritten));
        }
        else
        {
            // Fallback (should never happen with 66-byte buffer)
            writer.WriteStringValue(value.ToString());
        }
    }
}

