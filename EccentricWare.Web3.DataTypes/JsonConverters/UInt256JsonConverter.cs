using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace EccentricWare.Web3.DataTypes.JsonConverters;


/// <summary>
/// System.Text.Json converter for <see cref="EccentricWare.Web3.DataTypes.uint256"/>.
/// Supports JSON-RPC representations:
/// - String: "0x..." (hex) or "123" (decimal)
/// - Number: 123 (decimal)
/// Writes as a JSON string in minimal "0x" prefixed hex form.
/// </summary>
public sealed class UInt256JsonConverter : JsonConverter<EccentricWare.Web3.DataTypes.uint256>
{
    /// <summary>
    /// Reads a uint256 from a JSON token (string or number).
    /// </summary>
    public override EccentricWare.Web3.DataTypes.uint256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
        {
            // Fast path: unescaped, contiguous UTF-8 string value.
            if (!reader.ValueIsEscaped && !reader.HasValueSequence)
            {
                ReadOnlySpan<byte> utf8 = reader.ValueSpan;
                if (EccentricWare.Web3.DataTypes.uint256.TryParse(utf8, out var v))
                    return v;

                throw new JsonException("Invalid uint256 string value.");
            }

            // Slow path: escaped and/or non-contiguous.
            string? s = reader.GetString();
            if (s is null)
                throw new JsonException("Invalid uint256 string value (null).");

            if (EccentricWare.Web3.DataTypes.uint256.TryParse(s.AsSpan(), CultureInfo.InvariantCulture, out var v2))
                return v2;

            throw new JsonException("Invalid uint256 string value.");
        }

        if (reader.TokenType is JsonTokenType.Number)
        {
            // Numbers are exposed as raw UTF-8 digits in ValueSpan/ValueSequence.
            ReadOnlySpan<byte> utf8 = GetRawNumberUtf8(ref reader);
            if (EccentricWare.Web3.DataTypes.uint256.TryParse(utf8, out var v))
                return v;

            throw new JsonException("Invalid uint256 numeric value.");
        }

        if (reader.TokenType is JsonTokenType.Null)
            throw new JsonException("Cannot read null into uint256.");

        throw new JsonException($"Unsupported JSON token for uint256: {reader.TokenType}.");
    }

    /// <summary>
    /// Writes a uint256 as a JSON string in minimal 0x-prefixed hexadecimal format.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, EccentricWare.Web3.DataTypes.uint256 value, JsonSerializerOptions options)
    {
        Span<char> buffer = stackalloc char[66]; // "0x" + up to 64 hex digits.
        if (!value.TryFormat(buffer, out int written, "0x".AsSpan(), CultureInfo.InvariantCulture))
            throw new JsonException("Failed to format uint256.");

        writer.WriteStringValue(buffer.Slice(0, written));
    }

    /// <summary>
    /// Gets the raw UTF-8 bytes representing a JSON number token without allocating for the common contiguous case.
    /// </summary>
    private static ReadOnlySpan<byte> GetRawNumberUtf8(ref Utf8JsonReader reader)
    {
        if (!reader.HasValueSequence)
            return reader.ValueSpan;

        // ValueSequence can span segments. Copy into stack buffer (uint256 decimal max is 78 digits).
        ReadOnlySequence<byte> seq = reader.ValueSequence;
        if (seq.Length <= 0 || seq.Length > 80)
            throw new JsonException("uint256 numeric token length is invalid.");

        byte[] tmpReturn = new byte[(int)seq.Length];
        seq.CopyTo(tmpReturn);
        return tmpReturn;
    }
}