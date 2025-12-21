using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;


namespace EccentricWare.Web3.DataTypes.Utils.Uint256;

public enum UInt256ParseMode : byte
{
    EvmQuantityStrict = 0,   // "0x0" or "0x" + nonzero without leading zeros
    HexBytes32Strict = 1,    // exactly 32 bytes (64 nibbles) (optionally with 0x)
    DecimalUnsigned = 2      // digits only, no sign, no dot, no exponent
}

/// <summary>
/// Hot-path parsing utilities for <see cref="uint256"/> values from UTF-8 and <see cref="Utf8JsonReader"/>.
/// </summary>
internal static class UInt256Parser
{
    /// <summary>
    /// Tries to parse a UTF-8 encoded numeric value using the given mode.
    /// </summary>
    public static bool TryParseUtf8(ReadOnlySpan<byte> utf8Value, UInt256ParseMode parseMode, out uint256 parsedValue)
    {
        parsedValue = default;

        // If caller passed raw JSON string bytes with quotes, remove them.
        ByteUtils.TrimJsonQuotes(ref utf8Value);

        return parseMode switch
        {
            UInt256ParseMode.EvmQuantityStrict => TryParseEvmQuantityStrict(utf8Value, out parsedValue),
            UInt256ParseMode.HexBytes32Strict => TryParseHexBytes32Strict(utf8Value, out parsedValue),
            UInt256ParseMode.DecimalUnsigned => TryParseDecimalUnsigned(utf8Value, out parsedValue),
            _ => false
        };
    }

    /// <summary>
    /// Tries to read a JSON-RPC numeric value from the current token of a <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="jsonReader">Reader positioned on the value token.</param>
    /// <param name="parseMode">Parsing mode.</param>
    /// <param name="parsedValue">Parsed output.</param>
    /// <returns>True if parsed successfully; otherwise false.</returns>
    public static bool TryReadJsonRpcValue(ref Utf8JsonReader jsonReader, UInt256ParseMode parseMode, out uint256 parsedValue)
    {
        parsedValue = default;

        // EVM JSON-RPC quantities are strings. In strict mode, reject numbers.
        if (jsonReader.TokenType == JsonTokenType.String)
        {
            ReadOnlySpan<byte> utf8 = jsonReader.ValueSpan;

            if (!jsonReader.HasValueSequence)
                return TryParseUtf8(utf8, parseMode, out parsedValue);

            // Rare path: value spans multiple segments. Copy once into a pooled buffer.
            var seq = jsonReader.ValueSequence;
            int len = checked((int)seq.Length);

            byte[] rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                seq.CopyTo(rented);
                return TryParseUtf8(rented.AsSpan(0, len), parseMode, out parsedValue);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        if (jsonReader.TokenType == JsonTokenType.Number)
        {
            // Only allow decimal unsigned mode from JSON numbers; strict EVM should reject.
            if (parseMode != UInt256ParseMode.DecimalUnsigned)
                return false;

            if (!jsonReader.HasValueSequence)
                return TryParseDecimalUnsigned(jsonReader.ValueSpan, out parsedValue);

            var seq = jsonReader.ValueSequence;
            int len = checked((int)seq.Length);

            byte[] rented = ArrayPool<byte>.Shared.Rent(len);
            try
            {
                seq.CopyTo(rented);
                return TryParseDecimalUnsigned(rented.AsSpan(0, len), out parsedValue);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseEvmQuantityStrict(ReadOnlySpan<byte> utf8Value, out uint256 parsedValue)
    {
        parsedValue = default;

        if (!ByteUtils.Has0xPrefix(utf8Value))
            return false;

        ReadOnlySpan<byte> hexNibbles = utf8Value.Slice(2);
        if (hexNibbles.Length == 0)
            return false; // "0x" invalid

        if (hexNibbles.Length > 64)
            return false;

        // Canonical rule: no leading zeros unless exactly "0"
        if (hexNibbles.Length > 1 && hexNibbles[0] == (byte)'0')
            return false;

        return TryParseHexNibblesUtf8(hexNibbles, out parsedValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexBytes32Strict(ReadOnlySpan<byte> utf8Value, out uint256 parsedValue)
    {
        parsedValue = default;

        if (ByteUtils.Has0xPrefix(utf8Value))
            utf8Value = utf8Value.Slice(2);

        if (utf8Value.Length != 64)
            return false;

        return TryParseHexNibblesUtf8(utf8Value, out parsedValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseDecimalUnsigned(ReadOnlySpan<byte> utf8Value, out uint256 parsedValue)
    {
        parsedValue = uint256.Zero;

        if (utf8Value.Length == 0)
            return false;

        uint256 acc = uint256.Zero;

        for (int i = 0; i < utf8Value.Length; i++)
        {
            byte c = utf8Value[i];
            if ((uint)(c - (byte)'0') > 9)
                return false;

            if (!UInt256Math.TryMul10AddDigit(ref acc, (byte)(c - (byte)'0')))
                return false; // overflow
        }

        parsedValue = acc;
        return true;
    }

    /// <summary>
    /// Parses 1..64 hex nibbles (big-endian) into a <see cref="uint256"/> without allocations.
    /// </summary>
    /// <param name="hexNibblesUtf8">Hex digits without "0x".</param>
    /// <param name="parsedValue">Parsed output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryParseHexNibblesUtf8(ReadOnlySpan<byte> hexNibblesUtf8, out uint256 parsedValue)
    {
        parsedValue = default;
        if ((uint)hexNibblesUtf8.Length > 64) return false;
        if (hexNibblesUtf8.Length == 0) { parsedValue = uint256.Zero; return true; }

        ulong limb0 = 0, limb1 = 0, limb2 = 0, limb3 = 0;
        int endExclusive = hexNibblesUtf8.Length;

        // Read low limb (rightmost digits).
        int start = endExclusive > 16 ? endExclusive - 16 : 0;
        if (!ByteUtils.TryParseHexUInt64Utf8Variable(hexNibblesUtf8.Slice(start, endExclusive - start), out limb0)) return false;
        endExclusive = start;

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf8Variable(hexNibblesUtf8.Slice(start, endExclusive - start), out limb1)) return false;
            endExclusive = start;
        }

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf8Variable(hexNibblesUtf8.Slice(start, endExclusive - start), out limb2)) return false;
            endExclusive = start;
        }

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf8Variable(hexNibblesUtf8.Slice(start, endExclusive - start), out limb3)) return false;
        }

        parsedValue = new uint256(limb0, limb1, limb2, limb3);
        return true;
    }
}
