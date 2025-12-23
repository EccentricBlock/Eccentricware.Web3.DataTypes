using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// System.Text.Json converter for <see cref="EccentricWare.Web3.DataTypes.int256"/>.
/// Supports JSON-RPC representations:
/// - String: "0x..." (hex) or "-123" / "123" (decimal)
/// - Number: -123 / 123 (decimal)
/// Writes as a JSON string:
/// - Negative values: full-width 0x-prefixed 64-hex-digit two's complement
/// - Non-negative values: minimal 0x-prefixed hex
/// </summary>
public sealed class Int256JsonConverter : JsonConverter<EccentricWare.Web3.DataTypes.int256>
{
    /// <summary>
    /// Reads an <see cref="EccentricWare.Web3.DataTypes.int256"/> from a JSON token (string or number).
    /// </summary>
    public override EccentricWare.Web3.DataTypes.int256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
        {
            if (!reader.ValueIsEscaped && !reader.HasValueSequence)
            {
                ReadOnlySpan<byte> utf8 = reader.ValueSpan;
                if (EccentricWare.Web3.DataTypes.int256.TryParse(utf8, out var v))
                    return v;

                throw new JsonException("Invalid int256 string value.");
            }

            string? s = reader.GetString();
            if (s is null)
                throw new JsonException("Invalid int256 string value (null).");

            if (EccentricWare.Web3.DataTypes.int256.TryParse(s.AsSpan(), CultureInfo.InvariantCulture, out var v2))
                return v2;

            throw new JsonException("Invalid int256 string value.");
        }

        if (reader.TokenType is JsonTokenType.Number)
        {
            ReadOnlySpan<byte> utf8 = GetRawNumberUtf8(ref reader);
            if (EccentricWare.Web3.DataTypes.int256.TryParse(utf8, out var v))
                return v;

            throw new JsonException("Invalid int256 numeric value.");
        }

        if (reader.TokenType is JsonTokenType.Null)
            throw new JsonException("Cannot read null into int256.");

        throw new JsonException($"Unsupported JSON token for int256: {reader.TokenType}.");
    }

    /// <summary>
    /// Writes an <see cref="EccentricWare.Web3.DataTypes.int256"/> as a JSON string using hex.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, EccentricWare.Web3.DataTypes.int256 value, JsonSerializerOptions options)
    {
        // 0x + 64 digits for negative, or 0x + up to 64 for positive.
        Span<char> buffer = stackalloc char[66];
        ReadOnlySpan<char> format = "0x".AsSpan();

        // For negative, int256.TryFormat emits full-width two's complement for hex formats.
        if (!value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
            throw new JsonException("Failed to format int256.");

        writer.WriteStringValue(buffer.Slice(0, written));
    }

    /// <summary>
    /// Gets the raw UTF-8 bytes representing a JSON number token without allocating for the common contiguous case.
    /// </summary>
    private static ReadOnlySpan<byte> GetRawNumberUtf8(ref Utf8JsonReader reader)
    {
        if (!reader.HasValueSequence)
            return reader.ValueSpan;

        ReadOnlySequence<byte> seq = reader.ValueSequence;
        if (seq.Length <= 0 || seq.Length > 100)
            throw new JsonException("int256 numeric token length is invalid.");

        byte[] tmpReturn = new byte[(int)seq.Length];
        seq.CopyTo(tmpReturn);
        return tmpReturn;
    }
}
