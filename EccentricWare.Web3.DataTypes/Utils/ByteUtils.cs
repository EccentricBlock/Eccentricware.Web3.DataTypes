using System.Numerics;
using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Utils;

/// <summary>
/// Byte/UTF-8 and UTF-16 primitives for parsing and formatting without allocations.
/// </summary>
/// <remarks>
/// Metadata tags: [hotpath] [utf8] [utf16] [hex] [no-gc]
/// </remarks>
public static class ByteUtils
{
    private static ReadOnlySpan<byte> HexLowerUtf8 => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexUpperUtf8 => "0123456789ABCDEF"u8;

    private static ReadOnlySpan<char> HexLowerChars => "0123456789abcdef";
    private static ReadOnlySpan<char> HexUpperChars => "0123456789ABCDEF";

    /// <summary>
    /// Parses a UTF-8 hex nibble into 0..15; returns -1 on invalid input.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibbleUtf8(byte utf8Byte)
    {
        int v = utf8Byte;
        int digit = v - '0';
        int lower = (v | 0x20) - 'a' + 10;

        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    /// <summary>
    /// Parses a UTF-16 hex nibble into 0..15; returns -1 on invalid input.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibble(char c)
    {
        int v = c;
        int digit = v - '0';
        int lower = (v | 0x20) - 'a' + 10;

        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    /// <summary>
    /// Checks for a UTF-8 "0x" or "0X" prefix.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has0xPrefix(ReadOnlySpan<byte> utf8Value)
        => utf8Value.Length >= 2
           && utf8Value[0] == (byte)'0'
           && ((utf8Value[1] | 0x20) == (byte)'x');

    /// <summary>
    /// Checks for a UTF-16 "0x" or "0X" prefix.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has0xPrefix(ReadOnlySpan<char> text)
        => text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X');

    /// <summary>
    /// Trims surrounding double quotes in-place when parsing raw JSON string tokens.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [json]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TrimJsonQuotes(ref ReadOnlySpan<byte> utf8Value)
    {
        if (utf8Value.Length >= 2 && utf8Value[0] == (byte)'"' && utf8Value[^1] == (byte)'"')
            utf8Value = utf8Value.Slice(1, utf8Value.Length - 2);
    }

    /// <summary>
    /// Trims ASCII whitespace (' ', '\t', '\r', '\n') from both ends.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> TrimAsciiWhitespace(ReadOnlySpan<char> text)
    {
        int start = 0;
        int end = text.Length;

        while (start < end)
        {
            char c = text[start];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
            start++;
        }

        while (end > start)
        {
            char c = text[end - 1];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
            end--;
        }

        return text.Slice(start, end - start);
    }

    /// <summary>
    /// Parses a variable-width UTF-8 hex string (1..16 nibbles) into a <see cref="ulong"/>.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64Utf8Variable(ReadOnlySpan<byte> hexNibblesUtf8, out ulong value)
    {
        value = 0;
        if (hexNibblesUtf8.Length == 0 || hexNibblesUtf8.Length > 16) return false;

        for (int i = 0; i < hexNibblesUtf8.Length; i++)
        {
            int n = ParseHexNibbleUtf8(hexNibblesUtf8[i]);
            if (n < 0) return false;
            value = (value << 4) | (uint)n;
        }

        return true;
    }

    /// <summary>
    /// Parses a variable-width UTF-16 hex string (1..16 nibbles) into a <see cref="ulong"/>.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64Utf16Variable(ReadOnlySpan<char> hexNibbles, out ulong value)
    {
        value = 0;
        if (hexNibbles.Length == 0 || hexNibbles.Length > 16) return false;

        for (int i = 0; i < hexNibbles.Length; i++)
        {
            int n = ParseHexNibble(hexNibbles[i]);
            if (n < 0) return false;
            value = (value << 4) | (uint)n;
        }

        return true;
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> as fixed 16 hex digits into a UTF-8 destination.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHexUInt64Fixed16(ulong value, Span<byte> destinationUtf8, bool uppercase)
    {
        ReadOnlySpan<byte> table = uppercase ? HexUpperUtf8 : HexLowerUtf8;

        for (int i = 15; i >= 0; i--)
        {
            destinationUtf8[i] = table[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> as fixed 16 hex digits into a UTF-16 destination.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHexUInt64Fixed16(ulong value, Span<char> destinationChars, bool uppercase)
    {
        ReadOnlySpan<char> table = uppercase ? HexUpperChars : HexLowerChars;

        for (int i = 15; i >= 0; i--)
        {
            destinationChars[i] = table[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> as minimal hex digits (no leading zeros) into a UTF-8 destination.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf8] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteHexUInt64Minimal(ulong value, Span<byte> destinationUtf8, bool uppercase, out int bytesWritten)
    {
        if (value == 0)
        {
            if (destinationUtf8.Length < 1) { bytesWritten = 0; return false; }
            destinationUtf8[0] = (byte)'0';
            bytesWritten = 1;
            return true;
        }

        int leadingZeros = BitOperations.LeadingZeroCount(value);
        int nibbles = (64 - leadingZeros + 3) >> 2;

        if (destinationUtf8.Length < nibbles) { bytesWritten = 0; return false; }

        ReadOnlySpan<byte> table = uppercase ? HexUpperUtf8 : HexLowerUtf8;

        for (int i = nibbles - 1; i >= 0; i--)
        {
            destinationUtf8[i] = table[(int)(value & 0xF)];
            value >>= 4;
        }

        bytesWritten = nibbles;
        return true;
    }

    /// <summary>
    /// Writes a <see cref="ulong"/> as minimal hex digits (no leading zeros) into a UTF-16 destination.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [utf16] [hex]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteHexUInt64Minimal(ulong value, Span<char> destinationChars, bool uppercase, out int charsWritten)
    {
        if (value == 0)
        {
            if (destinationChars.Length < 1) { charsWritten = 0; return false; }
            destinationChars[0] = '0';
            charsWritten = 1;
            return true;
        }

        int leadingZeros = BitOperations.LeadingZeroCount(value);
        int nibbles = (64 - leadingZeros + 3) >> 2;

        if (destinationChars.Length < nibbles) { charsWritten = 0; return false; }

        ReadOnlySpan<char> table = uppercase ? HexUpperChars : HexLowerChars;

        for (int i = nibbles - 1; i >= 0; i--)
        {
            destinationChars[i] = table[(int)(value & 0xF)];
            value >>= 4;
        }

        charsWritten = nibbles;
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 hex span (1..8 nibbles) into a <see cref="uint"/>.
    /// </summary>
    /// <param name="utf8Hex">ASCII hex digits (no 0x prefix).</param>
    /// <param name="value">Parsed value on success.</param>
    /// <returns>True if parsed successfully; otherwise false.</returns>
    /// <remarks>
    /// Metadata tags: [hotpath] [utf8] [hex]
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt32Utf8(ReadOnlySpan<byte> utf8Hex, out uint value)
    {
        value = 0;

        int len = utf8Hex.Length;
        if ((uint)len is 0 or > 8)
            return false;

        uint acc = 0;
        for (int i = 0; i < len; i++)
        {
            int n = ParseHexNibbleUtf8(utf8Hex[i]);
            if (n < 0)
            {
                value = 0;
                return false;
            }

            acc = (acc << 4) | (uint)n;
        }

        value = acc;
        return true;
    }

    /// <summary>
    /// Parses exactly 16 UTF-16 hex digits into a <see cref="ulong"/>.
    /// </summary>
    /// <param name="hex16">Exactly 16 hex digits.</param>
    /// <returns>The parsed <see cref="ulong"/>.</returns>
    /// <exception cref="ArgumentException">If <paramref name="hex16"/> is not length 16.</exception>
    /// <exception cref="FormatException">If any character is not a valid hex digit.</exception>
    /// <remarks>
    /// Metadata tags: [hotpath] [utf16] [hex] [fixed-width]
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ParseHexUInt64(ReadOnlySpan<char> hex16)
    {
        if (hex16.Length != 16)
            throw new ArgumentException("Expected exactly 16 hex digits.", nameof(hex16));

        ulong acc = 0;
        for (int i = 0; i < 16; i++)
        {
            int n = ParseHexNibble(hex16[i]);
            if (n < 0)
                throw new FormatException("Invalid hex digit.");

            acc = (acc << 4) | (uint)n;
        }

        return acc;
    }

    /// <summary>
    /// Tries to parse exactly 16 UTF-16 hex digits into a <see cref="ulong"/>.
    /// </summary>
    /// <param name="hex16">Exactly 16 hex digits.</param>
    /// <param name="value">Parsed value on success.</param>
    /// <returns>True if parsed successfully; otherwise false.</returns>
    /// <remarks>
    /// Metadata tags: [hotpath] [utf16] [hex] [fixed-width]
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64(ReadOnlySpan<char> hex16, out ulong value)
    {
        value = 0;

        if (hex16.Length != 16)
            return false;

        ulong acc = 0;
        for (int i = 0; i < 16; i++)
        {
            int n = ParseHexNibble(hex16[i]);
            if (n < 0)
            {
                value = 0;
                return false;
            }

            acc = (acc << 4) | (uint)n;
        }

        value = acc;
        return true;
    }
}
