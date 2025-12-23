using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// System.Text.Json converter for <see cref="Hash32"/>.
/// Reads from either:
/// - EVM hex string (0x + 64 hex chars, or 64 hex chars),
/// - Solana Base58 string (decodes to 32 bytes),
/// - Base64 string (standard or URL-safe; decodes to 32 bytes).
/// Writes as lower-case hex with 0x prefix by default.
/// </summary>
/// <remarks>
/// Creates a converter that writes with a 0x prefix by default.
/// </remarks>
public sealed class Hash32JsonConverter(bool writeWith0xPrefix = true) : JsonConverter<Hash32>
{
    /// <summary>
    /// If true, writes "0x" prefixed lower-case hex (66 bytes/chars). If false, writes 64-char lower-case hex.
    /// </summary>
    public bool WriteWith0xPrefix { get; } = writeWith0xPrefix;

    /// <inheritdoc />
    public override Hash32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Accept strings and (optionally) raw bytes as base64.
        if (reader.TokenType == JsonTokenType.String)
        {
            // Fast-path: try get raw UTF-8 span without allocating a managed string.
            if (reader.HasValueSequence)
            {
                // Rare for typical JSON-RPC; keep correctness.
                string? s = reader.GetString();
                if (s is null)
                    throw new JsonException("Expected a string value for Hash32.");

                if (Hash32.TryParse(s.AsSpan(), out var valueFromString))
                    return valueFromString;

                // Unknown-source parsing: try base58/base64 fallbacks.
                ReadOnlySpan<byte> utf8 = System.Text.Encoding.UTF8.GetBytes(s);
                if (Hash32.TryParseAuto(utf8, out var valueAuto))
                    return valueAuto;

                throw new JsonException("Invalid Hash32 string.");
            }
            else
            {
                ReadOnlySpan<byte> utf8 = reader.ValueSpan;

                // Primary: safe unknown-source parsing (0x hex OR 64 hex OR base58 OR base64).
                if (Hash32.TryParseAuto(utf8, out var value))
                    return value;

                // If someone supplied raw 32 bytes but encoded as a JSON string (unlikely), reject.
                throw new JsonException("Invalid Hash32 string.");
            }
        }

        if (reader.TokenType == JsonTokenType.Null)
            return Hash32.Zero;

        throw new JsonException($"Unsupported token type {reader.TokenType} for Hash32.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Hash32 value, JsonSerializerOptions options)
    {
        // Write directly as UTF-8 without allocating a managed string.
        // Allocate a small stack buffer for the encoded text.
        int required = WriteWith0xPrefix ? 66 : 64;
        Span<byte> utf8 = required <= 128 ? stackalloc byte[required] : new byte[required];

        ReadOnlySpan<char> format = WriteWith0xPrefix ? "0x" : "x";
        if (!value.TryFormat(utf8, out int written, format, CultureInfo.InvariantCulture))
            throw new JsonException("Failed to format Hash32.");

        writer.WriteStringValue(utf8.Slice(0, written));
    }
}