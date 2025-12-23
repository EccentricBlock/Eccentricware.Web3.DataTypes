using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// High-throughput System.Text.Json converter for <see cref="Address"/>.
/// Avoids allocations by parsing directly from UTF-8 spans where possible.
/// </summary>
public sealed class AddressJsonConverter : JsonConverter<Address>
{
    /// <summary>Reads an <see cref="Address"/> from a JSON string token.</summary>
    public override Address Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for {nameof(Address)}.");

        // Addresses should never contain escape sequences; handle the rare case by falling back to GetString().
        if (reader.ValueIsEscaped)
        {
            string? text = reader.GetString();
            if (text is null || !Address.TryParse(text.AsSpan(), out var parsed))
                throw new JsonException($"Invalid {nameof(Address)} value.");

            return parsed;
        }

        if (!reader.HasValueSequence)
        {
            ReadOnlySpan<byte> utf8 = reader.ValueSpan;
            if (!Address.TryParse(utf8, out var parsed))
                throw new JsonException($"Invalid {nameof(Address)} value.");

            return parsed;
        }
        else
        {
            // Defensive copy for segmented sequences. Address strings are small (<= 44 chars).
            ReadOnlySequence<byte> sequence = reader.ValueSequence;
            int length = checked((int)sequence.Length);

            if ((uint)length > 128u)
                throw new JsonException($"Invalid {nameof(Address)} length.");

            Span<byte> scratch = length <= 128 ? stackalloc byte[length] : new byte[length];
            sequence.CopyTo(scratch);

            if (!Address.TryParse(scratch, out var parsed))
                throw new JsonException($"Invalid {nameof(Address)} value.");

            return parsed;
        }
    }

    /// <summary>Writes an <see cref="Address"/> as a JSON string value.</summary>
    public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
    {
        // Prefer span-based formatting to avoid string allocations.
        // EVM: 42 chars ("0x" + 40). Solana: up to 44 chars.
        Span<char> buffer = stackalloc char[Address.MaxBase58Length];

        if (!value.TryFormat(buffer, out int charsWritten, format: default, provider: null))
        {
            // Extremely unlikely; fallback to string.
            writer.WriteStringValue(value.ToString());
            return;
        }

        writer.WriteStringValue(buffer.Slice(0, charsWritten));
    }
}