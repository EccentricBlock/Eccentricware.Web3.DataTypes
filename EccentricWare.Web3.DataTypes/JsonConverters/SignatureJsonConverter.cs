using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// System.Text.Json converter for <see cref="Signature"/>.
/// Serialises as a 0x-prefixed lowercase hex string.
/// Deserialises from hex (optional 0x prefix) or Solana Base58 (if supported by <see cref="Signature.TryParse(ReadOnlySpan{byte}, out Signature)"/>).
/// </summary>
public sealed class SignatureJsonConverter : JsonConverter<Signature>
{
    /// <summary>
    /// Reads a <see cref="Signature"/> from a JSON string token.
    /// </summary>
    public override Signature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a JSON string for {nameof(Signature)}.");

        ReadOnlySpan<byte> utf8 = reader.HasValueSequence
            ? reader.ValueSequence.ToArray() // cold path
            : reader.ValueSpan;

        if (!Signature.TryParse(utf8, out Signature value))
            throw new JsonException($"Invalid {nameof(Signature)} encoding.");

        return value;
    }

    /// <summary>
    /// Writes a <see cref="Signature"/> as a 0x-prefixed lowercase hex JSON string.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Signature value, JsonSerializerOptions options)
    {
        // Max: 0x + 130 hex (EVM) = 132 bytes.
        int required = 2 + value.HexLength;

        Span<byte> buffer = required <= 256
            ? stackalloc byte[required]
            : new byte[required];

        if (!value.TryFormat(buffer, out int written, "0x", provider: CultureInfo.InvariantCulture))
            throw new JsonException($"Failed to format {nameof(Signature)}.");

        writer.WriteStringValue(buffer.Slice(0, written));
    }
}
