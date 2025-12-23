using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// Provides a high-throughput JSON converter for <see cref="HexBigInteger"/> using UTF-8 token parsing.
/// </summary>
public sealed class HexBigIntegerJsonConverter : JsonConverter<HexBigInteger>
{
    /// <inheritdoc />
    public override HexBigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType == JsonTokenType.String)
        {
            ReadOnlySpan<byte> utf8 = reader.HasValueSequence
                ? reader.ValueSequence.ToArray() // cold-ish; rare in practice, but safe
                : reader.ValueSpan;

            if (HexBigInteger.TryParseUtf8(utf8, out var value))
                return value;

            throw new JsonException("Invalid hex quantity string.");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            // Some clients emit numeric tokens; parse as decimal without allocating a string.
            if (reader.TryGetInt64(out long int64))
                return new HexBigInteger(new BigInteger(int64));

            // Fallback: decimal parsing from raw bytes (bounded).
            ReadOnlySpan<byte> utf8 = reader.HasValueSequence
                ? reader.ValueSequence.ToArray()
                : reader.ValueSpan;

            // As a conservative approach, reject very large numeric tokens here.
            if (utf8.Length > 128)
                throw new JsonException("Numeric token too large.");

            // Use BigInteger(string) would allocate; prefer rejecting or implementing a digit parser if needed.
            throw new JsonException("Unsupported numeric token.");
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, HexBigInteger value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[256];
        if (value.TryFormat(buffer, out int written, "0x", CultureInfo.InvariantCulture))
        {
            writer.WriteStringValue(buffer.Slice(0, written));
            return;
        }

        // Cold fallback.
        writer.WriteStringValue(value.ToString("0x", CultureInfo.InvariantCulture));
    }
}
