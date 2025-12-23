using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for <see cref="FunctionSelector"/> that parses from UTF-8 without string allocations.
/// Accepts "0x" prefixed or non-prefixed hex with exactly 8 digits.
/// </summary>
public sealed class FunctionSelectorJsonConverter : JsonConverter<FunctionSelector>
{
    /// <summary>
    /// Reads a selector from JSON.
    /// </summary>
    public override FunctionSelector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return FunctionSelector.Zero;

        if (reader.TokenType != JsonTokenType.String)
            ThrowHelper.ThrowJsonExceptionExpectedString(nameof(FunctionSelector));

        ReadOnlySpan<byte> utf8 = reader.HasValueSequence
            ? reader.ValueSequence.ToArray() // Rare; still correct. Prefer contiguous in practice.
            : reader.ValueSpan;

        if (!FunctionSelector.TryParse(utf8, out var selector))
            ThrowHelper.ThrowJsonExceptionInvalidValue(nameof(FunctionSelector));

        return selector;
    }

    /// <summary>
    /// Writes a selector to JSON as a canonical 0x-prefixed lowercase string.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, FunctionSelector value, JsonSerializerOptions options)
    {
        Span<byte> tmp = stackalloc byte[FunctionSelector.HexLength + 2];
        value.TryFormat(tmp, out int written, "0x".AsSpan(), provider: null);
        writer.WriteStringValue(tmp.Slice(0, written));
    }
}

