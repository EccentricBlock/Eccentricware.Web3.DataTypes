using EccentricWare.Web3.DataTypes.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for <see cref="InstructionDiscriminator"/> that parses from UTF-8 without string allocations.
/// Accepts "0x" prefixed or non-prefixed hex with exactly 16 digits.
/// </summary>
public sealed class InstructionDiscriminatorJsonConverter : JsonConverter<InstructionDiscriminator>
{
    /// <summary>
    /// Reads a discriminator from JSON.
    /// </summary>
    public override InstructionDiscriminator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return InstructionDiscriminator.Zero;

        if (reader.TokenType != JsonTokenType.String)
            ThrowHelper.ThrowJsonExceptionExpectedString(nameof(InstructionDiscriminator));

        ReadOnlySpan<byte> utf8 = reader.HasValueSequence
            ? reader.ValueSequence.ToArray() // Rare; still correct. Prefer contiguous in practice.
            : reader.ValueSpan;

        if (!InstructionDiscriminator.TryParse(utf8, out var d))
            ThrowHelper.ThrowJsonExceptionInvalidValue(nameof(InstructionDiscriminator));

        return d;
    }

    /// <summary>
    /// Writes a discriminator to JSON as a canonical 0x-prefixed lowercase string.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, InstructionDiscriminator value, JsonSerializerOptions options)
    {
        Span<byte> tmp = stackalloc byte[InstructionDiscriminator.HexLength + 2];
        value.TryFormat(tmp, out int written, "0x".AsSpan(), provider: null);
        writer.WriteStringValue(tmp.Slice(0, written));
    }
}