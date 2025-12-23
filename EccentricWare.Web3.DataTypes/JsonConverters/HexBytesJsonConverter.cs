using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// System.Text.Json converter for <see cref="HexBytes"/>.
/// </summary>
public sealed class HexBytesJsonConverter : JsonConverter<HexBytes>
{
    /// <summary>
    /// Reads a JSON string token as hex bytes (optionally 0x-prefixed).
    /// </summary>
    public override HexBytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return HexBytes.Empty;

        if (reader.TokenType != JsonTokenType.String)
            throw new FormatException("Expected String");

        ReadOnlySpan<byte> utf8 = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

        // If escaped, materialise the unescaped string into a temporary buffer.
        if (reader.ValueIsEscaped)
        {
            Span<byte> scratch = stackalloc byte[utf8.Length];
            int written = reader.CopyString(scratch);
            var slice = scratch.Slice(0, written);

            if (!HexBytes.TryParse(slice, out var parsed))
                ThrowHelper.ThrowFormatExceptionInvalidHex();

            return parsed;
        }

        if (!HexBytes.TryParse(utf8, out var value))
            ThrowHelper.ThrowFormatExceptionInvalidHex();

        return value;
    }

    /// <summary>
    /// Writes the value as a JSON string containing 0x-prefixed lowercase hex.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, HexBytes value, JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteStringValue("0x");
            return;
        }

        // Write as UTF-8 to avoid allocating a managed string.
        int required = value.HexLength + 2;

        // Use stackalloc for small values; pool for larger values.
        const int StackLimit = 512;
        if (required <= StackLimit)
        {
            Span<byte> utf8 = stackalloc byte[required];
            _ = value.TryFormat(utf8, out int written, default, CultureInfo.InvariantCulture);
            writer.WriteStringValue(utf8.Slice(0, written));
            return;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(required);
        try
        {
            Span<byte> utf8 = rented.AsSpan(0, required);
            _ = value.TryFormat(utf8, out int written, default, CultureInfo.InvariantCulture);
            writer.WriteStringValue(utf8.Slice(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}