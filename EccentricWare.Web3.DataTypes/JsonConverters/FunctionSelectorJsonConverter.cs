using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for FunctionSelector that serializes as hex string with 0x prefix.
/// Optimized for minimal allocations using UTF-8 spans.
/// </summary>
public sealed class FunctionSelectorJsonConverter : JsonConverter<FunctionSelector>
{
    public override FunctionSelector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert {reader.TokenType} to FunctionSelector");

        // Fast path: use raw UTF-8 span directly
        if (reader.HasValueSequence)
        {
            var str = reader.GetString();
            return str is null ? FunctionSelector.Zero : FunctionSelector.Parse(str);
        }

        ReadOnlySpan<byte> utf8 = reader.ValueSpan;
        
        // Handle 0x prefix
        if (utf8.Length >= 2 && utf8[0] == '0' && (utf8[1] == 'x' || utf8[1] == 'X'))
            utf8 = utf8.Slice(2);

        if (utf8.Length != FunctionSelector.HexLength)
            throw new JsonException($"FunctionSelector requires exactly {FunctionSelector.HexLength} hex characters");

        // Parse UTF-8 hex directly without string allocation
        Span<char> chars = stackalloc char[FunctionSelector.HexLength];
        for (int i = 0; i < FunctionSelector.HexLength; i++)
            chars[i] = (char)utf8[i];

        if (!FunctionSelector.TryParse(chars, out var result))
            throw new JsonException("Invalid hex string for FunctionSelector");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, FunctionSelector value, JsonSerializerOptions options)
    {
        // Write directly to UTF-8 buffer without string allocation
        Span<byte> buffer = stackalloc byte[FunctionSelector.HexLength + 2]; // "0x" + 8 hex chars
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            writer.WriteStringValue(buffer.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

