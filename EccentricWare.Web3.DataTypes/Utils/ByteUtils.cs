using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace EccentricWare.Web3.DataTypes.Utils;


/// <summary>
/// Provides shared low-level parsing and formatting helpers for Web3 datatypes.
/// These methods are designed to be allocation-free and predictable for hot-path parsing (e.g., JSON-RPC normalisation).
/// </summary>
public static class ByteUtils
{
    private static readonly ulong[] s_pow10 =
    {
        1UL,
        10UL,
        100UL,
        1_000UL,
        10_000UL,
        100_000UL,
        1_000_000UL,
        10_000_000UL,
        100_000_000UL,
        1_000_000_000UL,
        10_000_000_000UL,
        100_000_000_000UL,
        1_000_000_000_000UL,
        10_000_000_000_000UL,
        100_000_000_000_000UL,
        1_000_000_000_000_000UL,
        10_000_000_000_000_000UL,
        100_000_000_000_000_000UL,
        1_000_000_000_000_000_000UL,
        10_000_000_000_000_000_000UL
    };

    /// <summary>Lowercase hex alphabet for UTF-8 formatting.</summary>
    public static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;

    /// <summary>Uppercase hex alphabet for UTF-8 formatting.</summary>
    public static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    private static ReadOnlySpan<byte> Base58Alphabet => "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"u8;

    /// <summary>
    /// Returns 10^exponent for exponent in the range 0..19.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Pow10U64(int exponent) => s_pow10[exponent];

    /// <summary>
    /// Returns the number of bytes required to represent the specified 64-bit unsigned value (0 for zero).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(ulong value)
    {
        if (value == 0) return 0;
        return (64 - BitOperations.LeadingZeroCount(value) + 7) / 8;
    }

    /// <summary>
    /// Trims ASCII whitespace (space, tab, CR, LF) from both ends of a character span.
    /// Intended for hot paths where inputs originate from JSON/ASCII sources.
    /// </summary>
    public static ReadOnlySpan<char> TrimAsciiWhitespace(ReadOnlySpan<char> s)
    {
        int start = 0;
        int end = s.Length;

        while (start < end)
        {
            char c = s[start];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                break;
            start++;
        }

        while (end > start)
        {
            char c = s[end - 1];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                break;
            end--;
        }

        return s.Slice(start, end - start);
    }

    /// <summary>
    /// Trims ASCII whitespace (space, tab, CR, LF) from both ends of a UTF-8 span.
    /// </summary>
    public static ReadOnlySpan<byte> TrimAsciiWhitespaceUtf8(ReadOnlySpan<byte> s)
    {
        int start = 0;
        int end = s.Length;

        while (start < end)
        {
            byte c = s[start];
            if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                break;
            start++;
        }

        while (end > start)
        {
            byte c = s[end - 1];
            if (c != (byte)' ' && c != (byte)'\t' && c != (byte)'\r' && c != (byte)'\n')
                break;
            end--;
        }

        return s.Slice(start, end - start);
    }

    /// <summary>
    /// Attempts to trim a "0x" or "0X" prefix from a character span.
    /// </summary>
    /// <param name="s">Input span.</param>
    /// <param name="hexDigits">Span after removing the prefix, if present.</param>
    /// <returns>True if a prefix was removed; otherwise false.</returns>
    public static bool TryTrimHexPrefix(ReadOnlySpan<char> s, out ReadOnlySpan<char> hexDigits)
    {
        if (s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X'))
        {
            hexDigits = s.Slice(2);
            return true;
        }

        hexDigits = default;
        return false;
    }

    /// <summary>
    /// Attempts to trim a "0x" or "0X" prefix from a UTF-8 span.
    /// </summary>
    /// <param name="utf8">Input span.</param>
    /// <param name="hexDigits">Span after removing the prefix, if present.</param>
    /// <returns>True if a prefix was removed; otherwise false.</returns>
    public static bool TryTrimHexPrefixUtf8(ReadOnlySpan<byte> utf8, out ReadOnlySpan<byte> hexDigits)
    {
        if (utf8.Length >= 2 && utf8[0] == (byte)'0' && ((utf8[1] | 0x20) == (byte)'x'))
        {
            hexDigits = utf8.Slice(2);
            return true;
        }

        hexDigits = default;
        return false;
    }

    /// <summary>
    /// Parses a hex nibble (0-9, a-f, A-F). Returns -1 for invalid input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibble(char c)
    {
        int v = c;
        int d = v - '0';
        int l = (v | 0x20) - 'a' + 10;

        if ((uint)d <= 9) return d;
        if ((uint)(l - 10) <= 5) return l;
        return -1;
    }

    /// <summary>
    /// Parses a UTF-8 hex nibble (0-9, a-f, A-F). Returns -1 for invalid input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibbleUtf8(byte b)
    {
        int v = b;
        int d = v - '0';
        int l = (v | 0x20) - 'a' + 10;

        if ((uint)d <= 9) return d;
        if ((uint)(l - 10) <= 5) return l;
        return -1;
    }

    /// <summary>
    /// Tries to parse exactly 8 UTF-8 hex digits into a 32-bit unsigned integer.
    /// This is the preferred hot-path helper for EVM selectors (8 hex digits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt32Utf8Fixed8(ReadOnlySpan<byte> hex8Utf8, out uint value)
    {
        if (hex8Utf8.Length != 8)
        {
            value = 0;
            return false;
        }

        int n0 = ParseHexNibbleUtf8(hex8Utf8[0]);
        int n1 = ParseHexNibbleUtf8(hex8Utf8[1]);
        int n2 = ParseHexNibbleUtf8(hex8Utf8[2]);
        int n3 = ParseHexNibbleUtf8(hex8Utf8[3]);
        int n4 = ParseHexNibbleUtf8(hex8Utf8[4]);
        int n5 = ParseHexNibbleUtf8(hex8Utf8[5]);
        int n6 = ParseHexNibbleUtf8(hex8Utf8[6]);
        int n7 = ParseHexNibbleUtf8(hex8Utf8[7]);

        if ((n0 | n1 | n2 | n3 | n4 | n5 | n6 | n7) < 0)
        {
            value = 0;
            return false;
        }

        value =
            ((uint)n0 << 28) | ((uint)n1 << 24) | ((uint)n2 << 20) | ((uint)n3 << 16) |
            ((uint)n4 << 12) | ((uint)n5 << 8) | ((uint)n6 << 4) | (uint)n7;

        return true;
    }

    /// <summary>
    /// Tries to parse exactly 8 hex characters into a 32-bit unsigned integer.
    /// This is the preferred hot-path helper for EVM selectors (8 hex digits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt32CharsFixed8(ReadOnlySpan<char> hex8, out uint value)
    {
        if (hex8.Length != 8)
        {
            value = 0;
            return false;
        }

        int n0 = ParseHexNibble(hex8[0]);
        int n1 = ParseHexNibble(hex8[1]);
        int n2 = ParseHexNibble(hex8[2]);
        int n3 = ParseHexNibble(hex8[3]);
        int n4 = ParseHexNibble(hex8[4]);
        int n5 = ParseHexNibble(hex8[5]);
        int n6 = ParseHexNibble(hex8[6]);
        int n7 = ParseHexNibble(hex8[7]);

        if ((n0 | n1 | n2 | n3 | n4 | n5 | n6 | n7) < 0)
        {
            value = 0;
            return false;
        }

        value =
            ((uint)n0 << 28) | ((uint)n1 << 24) | ((uint)n2 << 20) | ((uint)n3 << 16) |
            ((uint)n4 << 12) | ((uint)n5 << 8) | ((uint)n6 << 4) | (uint)n7;

        return true;
    }

    /// <summary>
    /// Tries to parse 1..8 UTF-8 hex digits into a 32-bit unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt32Utf8(ReadOnlySpan<byte> hexDigitsUtf8, out uint value)
    {
        value = 0;

        int length = hexDigitsUtf8.Length;
        if ((uint)(length - 1) > 7) // 1..8
            return false;

        for (int i = 0; i < length; i++)
        {
            int nibble = ParseHexNibbleUtf8(hexDigitsUtf8[i]);
            if (nibble < 0)
                return false;

            value = (value << 4) | (uint)nibble;
        }

        return true;
    }

    /// <summary>
    /// Writes a 32-bit value as exactly 8 hex characters into the destination span.
    /// </summary>
    /// <param name="destination8">Destination span (must be exactly 8 characters).</param>
    /// <param name="value">Value to write.</param>
    /// <param name="uppercase">True to write uppercase A-F; otherwise lowercase.</param>
    public static void WriteHexUInt32CharsFixed8(Span<char> destination8, uint value, bool uppercase)
    {
        if (destination8.Length != 8)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination8), 8);

        ReadOnlySpan<char> alphabet = uppercase ? "0123456789ABCDEF" : "0123456789abcdef";

        for (int i = 7; i >= 0; i--)
        {
            destination8[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Writes a 32-bit value as exactly 8 hex bytes into the destination span.
    /// </summary>
    /// <param name="destination8">Destination span (must be exactly 8 bytes).</param>
    /// <param name="value">Value to write.</param>
    /// <param name="uppercase">True to write uppercase A-F; otherwise lowercase.</param>
    public static void WriteHexUInt32Utf8Fixed8(Span<byte> destination8, uint value, bool uppercase)
    {
        if (destination8.Length != 8)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination8), 8);

        ReadOnlySpan<byte> alphabet = uppercase ? HexBytesUpper : HexBytesLower;

        for (int i = 7; i >= 0; i--)
        {
            destination8[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Tries to parse 1..16 UTF-8 hex digits into a 64-bit unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64Utf8Variable(ReadOnlySpan<byte> hexDigits, out ulong value)
    {
        value = 0;
        int len = hexDigits.Length;
        if ((uint)(len - 1) > 15) return false;

        for (int i = 0; i < len; i++)
        {
            int nibble = ParseHexNibbleUtf8(hexDigits[i]);
            if (nibble < 0) return false;
            value = (value << 4) | (uint)nibble;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse 1..16 hex characters into a 64-bit unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64CharsVariable(ReadOnlySpan<char> hexDigits, out ulong value)
    {
        value = 0;
        int len = hexDigits.Length;
        if ((uint)(len - 1) > 15) return false;

        for (int i = 0; i < len; i++)
        {
            int nibble = ParseHexNibble(hexDigits[i]);
            if (nibble < 0) return false;
            value = (value << 4) | (uint)nibble;
        }

        return true;
    }

    /// <summary>
    /// Tries to parse exactly 16 hex characters into a 64-bit unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64CharsFixed16(ReadOnlySpan<char> hex16, out ulong value)
    {
        if (hex16.Length != 16)
        {
            value = 0;
            return false;
        }

        ulong v = 0;
        for (int i = 0; i < 16; i++)
        {
            int n = ParseHexNibble(hex16[i]);
            if (n < 0)
            {
                value = 0;
                return false;
            }
            v = (v << 4) | (uint)n;
        }

        value = v;
        return true;
    }

    /// <summary>
    /// Tries to parse exactly 16 UTF-8 hex bytes into a 64-bit unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64Utf8Fixed16(ReadOnlySpan<byte> hex16Utf8, out ulong value)
    {
        if (hex16Utf8.Length != 16)
        {
            value = 0;
            return false;
        }

        ulong v = 0;
        for (int i = 0; i < 16; i++)
        {
            int n = ParseHexNibbleUtf8(hex16Utf8[i]);
            if (n < 0)
            {
                value = 0;
                return false;
            }
            v = (v << 4) | (uint)n;
        }

        value = v;
        return true;
    }

    /// <summary>
    /// Writes a 64-bit value as exactly 16 hexadecimal characters into the destination span.
    /// </summary>
    /// <param name="destination">Destination span (must be length 16).</param>
    /// <param name="value">Value to write.</param>
    /// <param name="uppercase">True to write uppercase A-F; otherwise lowercase.</param>
    public static void WriteHexUInt64(Span<char> destination, ulong value, bool uppercase)
    {
        if (destination.Length != 16)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination), 16);

        ReadOnlySpan<char> alphabet = uppercase ? "0123456789ABCDEF" : "0123456789abcdef";

        for (int i = 15; i >= 0; i--)
        {
            destination[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Writes a 64-bit value as exactly 16 hexadecimal UTF-8 bytes into the destination span using the provided alphabet.
    /// </summary>
    /// <param name="destination">Destination span (must be length 16).</param>
    /// <param name="value">Value to write.</param>
    /// <param name="alphabet">Hex alphabet (must be length 16; e.g., "0123456789abcdef").</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHexUInt64Utf8(Span<byte> destination, ulong value, ReadOnlySpan<byte> alphabet)
    {
        if (destination.Length != 16)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination), 16);

        for (int i = 15; i >= 0; i--)
        {
            destination[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Returns true if the provided UTF-8 span contains only hex characters (0-9, a-f, A-F).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAllHexUtf8(ReadOnlySpan<byte> utf8)
    {
        for (int i = 0; i < utf8.Length; i++)
        {
            if (ParseHexNibbleUtf8(utf8[i]) < 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Decodes a Base64 (standard or URL-safe) UTF-8 payload into a destination buffer without allocations.
    /// URL-safe and missing padding are handled via a small stack buffer with a conservative size guard.
    /// </summary>
    /// <param name="utf8">Base64 text in UTF-8.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="bytesWritten">Number of bytes written.</param>
    /// <returns>True if decoding succeeded; otherwise false.</returns>
    public static bool TryDecodeBase64Utf8(ReadOnlySpan<byte> utf8, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;

        // Treat empty input as invalid. Base64.DecodeFromUtf8 reports Done with consumed==0 for empty input,
        // which would incorrectly be accepted below. Guard early to match callers' expectations.
        if (utf8.IsEmpty)
            return false;

        var status = Base64.DecodeFromUtf8(utf8, destination, out int consumed, out int written);
        if (status == OperationStatus.Done && consumed == utf8.Length)
        {
            bytesWritten = written;
            return true;
        }

        int len = utf8.Length;

        int pad = (4 - (len & 3)) & 3;
        int normalisedLen = len + pad;

        if ((uint)normalisedLen > 256u) return false;

        Span<byte> tmp = stackalloc byte[normalisedLen];
        for (int i = 0; i < len; i++)
        {
            byte c = utf8[i];
            tmp[i] = c switch
            {
                (byte)'-' => (byte)'+',
                (byte)'_' => (byte)'/',
                _ => c
            };
        }

        for (int i = 0; i < pad; i++)
            tmp[len + i] = (byte)'=';

        status = Base64.DecodeFromUtf8(tmp, destination, out consumed, out written);
        if (status == OperationStatus.Done && consumed == tmp.Length)
        {
            bytesWritten = written;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Decodes Base58 (Bitcoin/Solana alphabet) into exactly 32 bytes.
    /// Intended for Solana public keys / hashes where decoded payload MUST be exactly 32 bytes.
    /// </summary>
    /// <param name="utf8">Base58 text in UTF-8.</param>
    /// <param name="destination32">Destination span (must be exactly 32 bytes).</param>
    /// <returns>True if decoding succeeded without overflow; otherwise false.</returns>
    public static bool TryDecodeBase58To32(ReadOnlySpan<byte> utf8, Span<byte> destination32)
    {
        if (destination32.Length != 32)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination32), 32);

        destination32.Clear();
        if (utf8.IsEmpty) return false;

        ReadOnlySpan<byte> map = Base58ReverseMap;

        int leadingOnes = 0;
        while (leadingOnes < utf8.Length && utf8[leadingOnes] == (byte)'1')
            leadingOnes++;

        for (int i = leadingOnes; i < utf8.Length; i++)
        {
            int digit = map[utf8[i]];
            if (digit == 0xFF) return false;

            int carry = digit;
            for (int j = 31; j >= 0; j--)
            {
                int value = destination32[j] * 58 + carry;
                destination32[j] = (byte)value;
                carry = value >> 8;
            }

            if (carry != 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a read-only view of a struct's raw bytes without allocation.
    /// Intended for bulk operations where raw memory equality is valid.
    /// </summary>
    /// <typeparam name="T">Unmanaged struct type.</typeparam>
    /// <param name="value">Value to view.</param>
    public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T value)
        where T : unmanaged
    {
        ref T r = ref Unsafe.AsRef(in value);
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref r, 1));
    }

    /// <summary>
    /// Reads a <see cref="Vector256{T}"/> of bytes from a 32-byte span using an unaligned load.
    /// The span must be at least 32 bytes long.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> ReadVector256(ReadOnlySpan<byte> bytes32)
        => Unsafe.ReadUnaligned<Vector256<byte>>(ref MemoryMarshal.GetReference(bytes32));

    private static ReadOnlySpan<byte> Base58ReverseMap => s_base58ReverseMap;

    private static readonly byte[] s_base58ReverseMap = CreateBase58ReverseMap();

    /// <summary>
    /// Builds a reverse map for the Base58 alphabet for O(1) digit lookups.
    /// </summary>
    private static byte[] CreateBase58ReverseMap()
    {
        var map = new byte[256];
        Array.Fill(map, (byte)0xFF);

        ReadOnlySpan<byte> alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"u8;
        for (int i = 0; i < alphabet.Length; i++)
            map[alphabet[i]] = (byte)i;

        return map;
    }

    /// <summary>
    /// Returns a safe span conversion for nullable strings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> AsSpanSafe(this string? s)
        => s is null ? ReadOnlySpan<char>.Empty : s.AsSpan();

    /// <summary>
    /// Trims Unicode whitespace from both ends of a character span.
    /// </summary>
    public static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> s)
    {
        int start = 0;
        int end = s.Length;

        while (start < end && char.IsWhiteSpace(s[start])) start++;
        while (end > start && char.IsWhiteSpace(s[end - 1])) end--;

        return s.Slice(start, end - start);
    }

    /// <summary>
    /// Parses up to 64 hex digits into four 64-bit limbs (little-endian limb order u0..u3).
    /// </summary>
    /// <param name="hexDigits">Hex digits with no prefix.</param>
    /// <param name="u0">Least significant limb.</param>
    /// <param name="u1">Second limb.</param>
    /// <param name="u2">Third limb.</param>
    /// <param name="u3">Most significant limb.</param>
    public static bool TryParseUInt256HexUtf8(ReadOnlySpan<byte> hexDigits, out ulong u0, out ulong u1, out ulong u2, out ulong u3)
    {
        u0 = u1 = u2 = u3 = 0;

        int len = hexDigits.Length;
        if (len == 0) return true;
        if (len > 64) return false;

        // Parse from right (least significant digits).
        ReadOnlySpan<byte> span = hexDigits;

        if (!TakeLastHex(span, out span, out u0)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u1)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u2)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u3)) return false;
        return span.Length == 0;

        static bool TakeLastHex(ReadOnlySpan<byte> input, out ReadOnlySpan<byte> remaining, out ulong value)
        {
            int take = input.Length >= 16 ? 16 : input.Length;
            remaining = input.Slice(0, input.Length - take);
            ReadOnlySpan<byte> tail = input.Slice(input.Length - take, take);
            return TryParseHexUInt64Utf8Variable(tail, out value);
        }
    }

    /// <summary>
    /// Parses up to 64 hex digits into four 64-bit limbs (little-endian limb order u0..u3).
    /// </summary>
    public static bool TryParseUInt256HexChars(ReadOnlySpan<char> hexDigits, out ulong u0, out ulong u1, out ulong u2, out ulong u3)
    {
        u0 = u1 = u2 = u3 = 0;

        int len = hexDigits.Length;
        if (len == 0) return true;
        if (len > 64) return false;

        ReadOnlySpan<char> span = hexDigits;

        if (!TakeLastHex(span, out span, out u0)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u1)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u2)) return false;
        if (span.Length == 0) return true;

        if (!TakeLastHex(span, out span, out u3)) return false;
        return span.Length == 0;

        static bool TakeLastHex(ReadOnlySpan<char> input, out ReadOnlySpan<char> remaining, out ulong value)
        {
            int take = input.Length >= 16 ? 16 : input.Length;
            remaining = input.Slice(0, input.Length - take);
            ReadOnlySpan<char> tail = input.Slice(input.Length - take, take);
            return TryParseHexUInt64CharsVariable(tail, out value);
        }
    }

    /// <summary>
    /// Parses UTF-8 decimal digits into four 64-bit limbs (little-endian limb order u0..u3).
    /// Returns false if input is invalid or exceeds 256 bits.
    /// </summary>
    public static bool TryParseUInt256DecimalUtf8(ReadOnlySpan<byte> digitsUtf8, out ulong u0, out ulong u1, out ulong u2, out ulong u3)
    {
        u0 = u1 = u2 = u3 = 0;

        if (digitsUtf8.Length == 0)
            return false;

        for (int i = 0; i < digitsUtf8.Length; i++)
        {
            uint digit = (uint)(digitsUtf8[i] - (byte)'0');
            if (digit > 9) return false;

            UInt128 carry = digit;

            UInt128 p0 = (UInt128)u0 * 10u + carry;
            u0 = (ulong)p0;
            carry = p0 >> 64;

            UInt128 p1 = (UInt128)u1 * 10u + carry;
            u1 = (ulong)p1;
            carry = p1 >> 64;

            UInt128 p2 = (UInt128)u2 * 10u + carry;
            u2 = (ulong)p2;
            carry = p2 >> 64;

            UInt128 p3 = (UInt128)u3 * 10u + carry;
            u3 = (ulong)p3;
            carry = p3 >> 64;

            if (carry != 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses decimal digits into four 64-bit limbs (little-endian limb order u0..u3).
    /// Returns false if input is invalid or exceeds 256 bits.
    /// </summary>
    public static bool TryParseUInt256DecimalChars(ReadOnlySpan<char> digits, out ulong u0, out ulong u1, out ulong u2, out ulong u3)
    {
        u0 = u1 = u2 = u3 = 0;

        if (digits.Length == 0)
            return false;

        for (int i = 0; i < digits.Length; i++)
        {
            uint digit = (uint)(digits[i] - '0');
            if (digit > 9) return false;

            UInt128 carry = digit;

            UInt128 p0 = (UInt128)u0 * 10u + carry;
            u0 = (ulong)p0;
            carry = p0 >> 64;

            UInt128 p1 = (UInt128)u1 * 10u + carry;
            u1 = (ulong)p1;
            carry = p1 >> 64;

            UInt128 p2 = (UInt128)u2 * 10u + carry;
            u2 = (ulong)p2;
            carry = p2 >> 64;

            UInt128 p3 = (UInt128)u3 * 10u + carry;
            u3 = (ulong)p3;
            carry = p3 >> 64;

            if (carry != 0)
                return false;
        }

        return true;
    }


    /// <summary>
    /// Tries to parse 1..16 hexadecimal characters into a 64-bit unsigned integer.
    /// The input must contain only hex digits (0-9, a-f, A-F) and must be between 1 and 16 characters long.
    /// </summary>
    /// <param name="hexDigits">The hexadecimal digits span (no 0x prefix).</param>
    /// <param name="value">The parsed 64-bit value if successful; otherwise 0.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ParseHexUInt64CharsVariable(ReadOnlySpan<char> hexDigits)
    {
        ulong value = 0;

        int length = hexDigits.Length;
        if ((uint)(length - 1) > 15) // 1..16
            return ulong.MaxValue;

        for (int i = 0; i < length; i++)
        {
            int nibble = ParseHexNibble(hexDigits[i]);
            if (nibble < 0)
                return ulong.MaxValue;

            value = (value << 4) | (uint)nibble;
        }

        return value;
    }

    /// <summary>
    /// Trims a leading '+' or '-' sign from a character span.
    /// If no sign is present, the output span is the original input and <paramref name="isNegative"/> is false.
    /// </summary>
    /// <param name="s">Input span.</param>
    /// <param name="isNegative">True if a leading '-' was present.</param>
    /// <param name="unsignedSpan">The span with the leading sign removed (if present).</param>
    /// <returns>True if the span is valid for further parsing; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryTrimLeadingSign(ReadOnlySpan<char> s, out bool isNegative, out ReadOnlySpan<char> unsignedSpan)
    {
        isNegative = false;

        if (s.Length == 0)
        {
            unsignedSpan = default;
            return false;
        }

        char c = s[0];
        if (c == '+')
        {
            unsignedSpan = s.Slice(1);
            return true;
        }

        if (c == '-')
        {
            isNegative = true;
            unsignedSpan = s.Slice(1);
            return true;
        }

        unsignedSpan = s;
        return true;
    }

    /// <summary>
    /// Trims a leading '+' or '-' sign from a UTF-8 span.
    /// If no sign is present, the output span is the original input and <paramref name="isNegative"/> is false.
    /// </summary>
    /// <param name="utf8">Input span.</param>
    /// <param name="isNegative">True if a leading '-' was present.</param>
    /// <param name="unsignedSpan">The span with the leading sign removed (if present).</param>
    /// <returns>True if the span is valid for further parsing; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryTrimLeadingSignUtf8(ReadOnlySpan<byte> utf8, out bool isNegative, out ReadOnlySpan<byte> unsignedSpan)
    {
        isNegative = false;

        if (utf8.Length == 0)
        {
            unsignedSpan = default;
            return false;
        }

        byte c = utf8[0];
        if (c == (byte)'+')
        {
            unsignedSpan = utf8.Slice(1);
            return true;
        }

        if (c == (byte)'-')
        {
            isNegative = true;
            unsignedSpan = utf8.Slice(1);
            return true;
        }

        unsignedSpan = utf8;
        return true;
    }

    /// <summary>
    /// Computes the two's complement negation of a 256-bit unsigned magnitude (u0..u3) without allocating.
    /// This is used to convert a positive magnitude into its negative two's complement representation.
    /// </summary>
    /// <param name="u0">Least significant limb.</param>
    /// <param name="u1">Second limb.</param>
    /// <param name="u2">Third limb.</param>
    /// <param name="u3">Most significant limb.</param>
    /// <param name="r0">Negated least significant limb.</param>
    /// <param name="r1">Negated second limb.</param>
    /// <param name="r2">Negated third limb.</param>
    /// <param name="r3">Negated most significant limb.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NegateTwosComplement256(ulong u0, ulong u1, ulong u2, ulong u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3)
    {
        ulong n0 = ~u0;
        ulong n1 = ~u1;
        ulong n2 = ~u2;
        ulong n3 = ~u3;

        r0 = n0 + 1UL;
        ulong c0 = r0 == 0 ? 1UL : 0UL;

        r1 = n1 + c0;
        ulong c1 = (c0 == 1 && r1 == 0) ? 1UL : 0UL;

        r2 = n2 + c1;
        ulong c2 = (c1 == 1 && r2 == 0) ? 1UL : 0UL;

        r3 = n3 + c2;
    }


    /// <summary>
    /// Returns true if every character is an ASCII hex digit (0-9, a-f, A-F).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAllHexChars(ReadOnlySpan<char> chars)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            if (ParseHexNibble(chars[i]) < 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Trims surrounding JSON string quotes (<c>"..."</c>) from a UTF-8 span if present.
    /// Returns true if the resulting span is non-empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnquoteJsonUtf8(ReadOnlySpan<byte> utf8, out ReadOnlySpan<byte> unquoted)
    {
        if (utf8.Length >= 2 && utf8[0] == (byte)'"' && utf8[^1] == (byte)'"')
            utf8 = utf8.Slice(1, utf8.Length - 2);

        unquoted = utf8;
        return !unquoted.IsEmpty;
    }

    /// <summary>
    /// Writes a 64-bit value as exactly 16 hex characters.
    /// </summary>
    public static void WriteHexUInt64CharsFixed16(Span<char> destination16, ulong value, bool uppercase)
    {
        if (destination16.Length != 16)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination16), 16);

        ReadOnlySpan<char> alphabet = uppercase ? "0123456789ABCDEF" : "0123456789abcdef";
        for (int i = 15; i >= 0; i--)
        {
            destination16[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Writes a 64-bit value as exactly 16 UTF-8 hex bytes using a supplied alphabet.
    /// </summary>
    public static void WriteHexUInt64Utf8Fixed16(Span<byte> destination16, ulong value, ReadOnlySpan<byte> alphabet)
    {
        if (destination16.Length != 16)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination16), 16);

        for (int i = 15; i >= 0; i--)
        {
            destination16[i] = alphabet[(int)(value & 0xFu)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Counts leading zero bytes in a 256-bit big-endian value represented by four 64-bit limbs.
    /// Limb order is most-significant to least-significant (<paramref name="u0Msb"/>.. <paramref name="u3Lsb"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountLeadingZeroBytes256BigEndian(ulong u0Msb, ulong u1, ulong u2, ulong u3Lsb)
    {
        if (u0Msb != 0) return BitOperations.LeadingZeroCount(u0Msb) >> 3;
        if (u1 != 0) return 8 + (BitOperations.LeadingZeroCount(u1) >> 3);
        if (u2 != 0) return 16 + (BitOperations.LeadingZeroCount(u2) >> 3);
        if (u3Lsb != 0) return 24 + (BitOperations.LeadingZeroCount(u3Lsb) >> 3);
        return 32;
    }

    /// <summary>
    /// Decodes Base58 (Solana/Bitcoin alphabet) into a 256-bit big-endian value and enforces canonical 32-byte encoding.
    /// </summary>
    /// <remarks>
    /// Canonical enforcement requires the number of leading '1' characters to equal the number of leading zero bytes
    /// in the decoded 32-byte value. This rejects under-length and over-length encodings for Solana public keys.
    /// </remarks>
    public static bool TryDecodeBase58ToUInt256BigEndian(
        ReadOnlySpan<byte> base58Utf8,
        out ulong u0Msb,
        out ulong u1,
        out ulong u2,
        out ulong u3Lsb)
    {
        u0Msb = u1 = u2 = u3Lsb = 0;

        if (base58Utf8.IsEmpty)
            return false;

        // Solana public keys are typically 32..44 chars; cap early for DoS hygiene.
        if (base58Utf8.Length > Address.MaxBase58Length)
            return false;

        int leadingOnes = 0;
        while (leadingOnes < base58Utf8.Length && base58Utf8[leadingOnes] == (byte)'1')
            leadingOnes++;

        if (leadingOnes > 32) // cannot represent >32 leading zero bytes
            return false;

        ReadOnlySpan<byte> map = Base58ReverseMap;

        for (int i = leadingOnes; i < base58Utf8.Length; i++)
        {
            byte c = base58Utf8[i];
            int digit = map[c];
            if (digit == 0xFF)
                return false;

            if (!MulAdd58UInt256(ref u0Msb, ref u1, ref u2, ref u3Lsb, (uint)digit))
                return false;
        }

        int leadingZeroBytes = CountLeadingZeroBytes256BigEndian(u0Msb, u1, u2, u3Lsb);
        return leadingOnes == leadingZeroBytes;

        static bool MulAdd58UInt256(ref ulong u0Msb, ref ulong u1, ref ulong u2, ref ulong u3Lsb, uint addDigit)
        {
            UInt128 p3 = (UInt128)u3Lsb * 58u + addDigit;
            u3Lsb = (ulong)p3;
            ulong carry = (ulong)(p3 >> 64);

            UInt128 p2 = (UInt128)u2 * 58u + carry;
            u2 = (ulong)p2;
            carry = (ulong)(p2 >> 64);

            UInt128 p1 = (UInt128)u1 * 58u + carry;
            u1 = (ulong)p1;
            carry = (ulong)(p1 >> 64);

            UInt128 p0 = (UInt128)u0Msb * 58u + carry;
            u0Msb = (ulong)p0;
            carry = (ulong)(p0 >> 64);

            return carry == 0;
        }
    }

    /// <summary>
    /// Encodes a 256-bit big-endian value to Base58 UTF-8 bytes.
    /// Returns the number of bytes written, or -1 if <paramref name="destination"/> is too small.
    /// </summary>
    public static int EncodeBase58UInt256BigEndianToUtf8(
        ulong u0Msb,
        ulong u1,
        ulong u2,
        ulong u3Lsb,
        Span<byte> destination)
    {
        // All-zero 32-byte value encodes as 32 '1' characters.
        if ((u0Msb | u1 | u2 | u3Lsb) == 0)
        {
            if (destination.Length < 32)
                return -1;

            destination.Slice(0, 32).Fill((byte)'1');
            return 32;
        }

        int leadingZeroBytes = CountLeadingZeroBytes256BigEndian(u0Msb, u1, u2, u3Lsb);

        // Convert by repeated div/mod 58 into a temporary buffer (reversed digits).
        Span<byte> tmp = stackalloc byte[Address.MaxBase58Length];
        int tmpLen = 0;

        ulong v0 = u0Msb, v1 = u1, v2 = u2, v3 = u3Lsb;

        while ((v0 | v1 | v2 | v3) != 0)
        {
            uint rem = DivMod58(ref v0, ref v1, ref v2, ref v3);
            tmp[tmpLen++] = Base58Alphabet[(int)rem];
        }

        int totalLen = leadingZeroBytes + tmpLen;
        if (destination.Length < totalLen)
            return -1;

        // Leading '1's.
        destination.Slice(0, leadingZeroBytes).Fill((byte)'1');

        // Reverse digits into output.
        int outPos = leadingZeroBytes;
        for (int i = tmpLen - 1; i >= 0; i--)
            destination[outPos++] = tmp[i];

        return totalLen;

        static uint DivMod58(ref ulong u0, ref ulong u1, ref ulong u2, ref ulong u3)
        {
            ulong rem = 0;

            u0 = DivMod64(u0, ref rem);
            u1 = DivMod64(u1, ref rem);
            u2 = DivMod64(u2, ref rem);
            u3 = DivMod64(u3, ref rem);

            return (uint)rem;

            static ulong DivMod64(ulong value, ref ulong remainder)
            {
                UInt128 dividend = ((UInt128)remainder << 64) | value;
                UInt128 q = dividend / 58u;
                remainder = (ulong)(dividend - (q * 58u));
                return (ulong)q;
            }
        }
    }

    /// <summary>
    /// Calculates the number of bytes produced when decoding a hex digit sequence.
    /// </summary>
    /// <param name="hexDigitCount">Number of hex digits (no prefix).</param>
    /// <param name="allowOddLength">True to allow odd-length digits by left-padding with a zero nibble.</param>
    /// <returns>Decoded byte count, or -1 if invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetHexDecodedByteCount(int hexDigitCount, bool allowOddLength)
    {
        if (hexDigitCount < 0) return -1;
        if (!allowOddLength && (hexDigitCount & 1) != 0) return -1;
        return (hexDigitCount + 1) >> 1;
    }

    /// <summary>
    /// Decodes hex characters into bytes.
    /// </summary>
    /// <param name="hexDigits">Hex digits (no 0x prefix).</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="bytesWritten">Number of bytes written.</param>
    /// <param name="allowOddLength">True to allow odd-length digits by left-padding with a zero nibble.</param>
    public static bool TryDecodeHexChars(ReadOnlySpan<char> hexDigits, Span<byte> destination, out int bytesWritten, bool allowOddLength)
    {
        bytesWritten = 0;

        if (hexDigits.IsEmpty)
            return true;

        int required = GetHexDecodedByteCount(hexDigits.Length, allowOddLength);
        if (required < 0 || destination.Length < required)
            return false;

        int src = 0;
        int dst = 0;

        if ((hexDigits.Length & 1) != 0)
        {
            if (!allowOddLength)
                return false;

            int loNibble = ParseHexNibble(hexDigits[src++]);
            if (loNibble < 0)
                return false;

            destination[dst++] = (byte)loNibble; // 0x0? form
        }

        for (; src < hexDigits.Length; src += 2)
        {
            int hi = ParseHexNibble(hexDigits[src]);
            int lo = ParseHexNibble(hexDigits[src + 1]);
            if ((hi | lo) < 0)
                return false;

            destination[dst++] = (byte)((hi << 4) | lo);
        }

        bytesWritten = dst;
        return true;
    }

    /// <summary>
    /// Decodes UTF-8 hex bytes into bytes.
    /// </summary>
    /// <param name="hexDigitsUtf8">Hex digits (no 0x prefix) in UTF-8.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="bytesWritten">Number of bytes written.</param>
    /// <param name="allowOddLength">True to allow odd-length digits by left-padding with a zero nibble.</param>
    public static bool TryDecodeHexUtf8(ReadOnlySpan<byte> hexDigitsUtf8, Span<byte> destination, out int bytesWritten, bool allowOddLength)
    {
        bytesWritten = 0;

        if (hexDigitsUtf8.IsEmpty)
            return true;

        int required = GetHexDecodedByteCount(hexDigitsUtf8.Length, allowOddLength);
        if (required < 0 || destination.Length < required)
            return false;

        int src = 0;
        int dst = 0;

        if ((hexDigitsUtf8.Length & 1) != 0)
        {
            if (!allowOddLength)
                return false;

            int loNibble = ParseHexNibbleUtf8(hexDigitsUtf8[src++]);
            if (loNibble < 0)
                return false;

            destination[dst++] = (byte)loNibble; // 0x0? form
        }

        for (; src < hexDigitsUtf8.Length; src += 2)
        {
            int hi = ParseHexNibbleUtf8(hexDigitsUtf8[src]);
            int lo = ParseHexNibbleUtf8(hexDigitsUtf8[src + 1]);
            if ((hi | lo) < 0)
                return false;

            destination[dst++] = (byte)((hi << 4) | lo);
        }

        bytesWritten = dst;
        return true;
    }

    /// <summary>
    /// Encodes bytes into hex characters.
    /// </summary>
    /// <param name="sourceBytes">Bytes to encode.</param>
    /// <param name="destination">Destination buffer (must be length = sourceBytes.Length * 2).</param>
    /// <param name="uppercase">True for uppercase A-F; otherwise lowercase.</param>
    public static bool TryEncodeHexChars(ReadOnlySpan<byte> sourceBytes, Span<char> destination, bool uppercase)
    {
        if (destination.Length < (sourceBytes.Length << 1))
            return false;

        ReadOnlySpan<byte> alphabet = uppercase ? HexBytesUpper : HexBytesLower;

        for (int i = 0; i < sourceBytes.Length; i++)
        {
            byte b = sourceBytes[i];
            destination[(i << 1) + 0] = (char)alphabet[b >> 4];
            destination[(i << 1) + 1] = (char)alphabet[b & 0x0F];
        }

        return true;
    }

    /// <summary>
    /// Encodes bytes into UTF-8 hex bytes.
    /// </summary>
    /// <param name="sourceBytes">Bytes to encode.</param>
    /// <param name="destination">Destination buffer (must be length = sourceBytes.Length * 2).</param>
    /// <param name="uppercase">True for uppercase A-F; otherwise lowercase.</param>
    public static bool TryEncodeHexUtf8(ReadOnlySpan<byte> sourceBytes, Span<byte> destination, bool uppercase)
    {
        if (destination.Length < (sourceBytes.Length << 1))
            return false;

        ReadOnlySpan<byte> alphabet = uppercase ? HexBytesUpper : HexBytesLower;

        for (int i = 0; i < sourceBytes.Length; i++)
        {
            byte b = sourceBytes[i];
            destination[(i << 1) + 0] = alphabet[b >> 4];
            destination[(i << 1) + 1] = alphabet[b & 0x0F];
        }

        return true;
    }

    /// <summary>
    /// Removes surrounding JSON string quotes (<c>"..."</c>) from a UTF-8 span if present.
    /// Unlike typical “Try*” helpers, this returns the (possibly empty) resulting span.
    /// </summary>
    /// <param name="utf8">Input UTF-8 span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> UnquoteJsonStringUtf8(ReadOnlySpan<byte> utf8)
        => utf8.Length >= 2 && utf8[0] == (byte)'"' && utf8[^1] == (byte)'"'
            ? utf8.Slice(1, utf8.Length - 2)
            : utf8;


    /// <summary>
    /// Reads up to 8 bytes starting at <paramref name="startIndex"/> and returns them as a little-endian <see cref="ulong"/>.
    /// Missing bytes are treated as zero. Intended for O(1) hash sampling.
    /// </summary>
    /// <param name="sourceBytes">Source bytes.</param>
    /// <param name="startIndex">Start index for sampling.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SampleUInt64LittleEndian(ReadOnlySpan<byte> sourceBytes, int startIndex)
    {
        if ((uint)startIndex >= (uint)sourceBytes.Length)
            return 0;

        int remaining = sourceBytes.Length - startIndex;
        if (remaining >= 8)
            return BinaryPrimitives.ReadUInt64LittleEndian(sourceBytes.Slice(startIndex, 8));

        ulong value = 0;
        for (int i = 0; i < remaining; i++)
            value |= ((ulong)sourceBytes[startIndex + i]) << (i * 8);

        return value;
    }

    /// <summary>
    /// Tries to parse exactly 2 UTF-8 hex digits into a byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexByteUtf8Fixed2(ReadOnlySpan<byte> hex2Utf8, out byte value)
    {
        if (hex2Utf8.Length != 2)
        {
            value = 0;
            return false;
        }

        int hi = ParseHexNibbleUtf8(hex2Utf8[0]);
        int lo = ParseHexNibbleUtf8(hex2Utf8[1]);
        if ((hi | lo) < 0)
        {
            value = 0;
            return false;
        }

        value = (byte)((hi << 4) | lo);
        return true;
    }

    /// <summary>
    /// Tries to parse exactly 2 hex characters into a byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexByteCharsFixed2(ReadOnlySpan<char> hex2, out byte value)
    {
        if (hex2.Length != 2)
        {
            value = 0;
            return false;
        }

        int hi = ParseHexNibble(hex2[0]);
        int lo = ParseHexNibble(hex2[1]);
        if ((hi | lo) < 0)
        {
            value = 0;
            return false;
        }

        value = (byte)((hi << 4) | lo);
        return true;
    }

    /// <summary>
    /// Writes a byte as exactly 2 hex characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHexByteCharsFixed2(Span<char> destination2, byte value, bool uppercase)
    {
        if (destination2.Length != 2)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination2), 2);

        ReadOnlySpan<char> alphabet = uppercase ? "0123456789ABCDEF" : "0123456789abcdef";
        destination2[0] = alphabet[value >> 4];
        destination2[1] = alphabet[value & 0x0F];
    }

    /// <summary>
    /// Writes a byte as exactly 2 hex UTF-8 bytes using the provided alphabet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHexByteUtf8Fixed2(Span<byte> destination2, byte value, ReadOnlySpan<byte> alphabet)
    {
        if (destination2.Length != 2)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination2), 2);

        destination2[0] = alphabet[value >> 4];
        destination2[1] = alphabet[value & 0x0F];
    }

    /// <summary>
    /// Decodes Base58 (Solana/Bitcoin alphabet) into exactly 64 bytes and enforces canonical encoding.
    /// Canonical enforcement requires the number of leading '1' characters to equal the number of leading zero bytes.
    /// </summary>
    public static bool TryDecodeBase58To64(ReadOnlySpan<byte> base58Utf8, Span<byte> destination64)
    {
        if (destination64.Length != 64)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination64), 64);

        destination64.Clear();
        if (base58Utf8.IsEmpty) return false;

        ReadOnlySpan<byte> map = Base58ReverseMap;

        int leadingOnes = 0;
        while (leadingOnes < base58Utf8.Length && base58Utf8[leadingOnes] == (byte)'1')
            leadingOnes++;

        if (leadingOnes > 64)
            return false;

        for (int i = leadingOnes; i < base58Utf8.Length; i++)
        {
            int digit = map[base58Utf8[i]];
            if (digit == 0xFF) return false;

            int carry = digit;
            for (int j = 63; j >= 0; j--)
            {
                int acc = destination64[j] * 58 + carry;
                destination64[j] = (byte)acc;
                carry = acc >> 8;
            }

            if (carry != 0)
                return false;
        }

        int leadingZeroBytes = 0;
        while (leadingZeroBytes < 64 && destination64[leadingZeroBytes] == 0)
            leadingZeroBytes++;

        return leadingOnes == leadingZeroBytes;
    }

    /// <summary>
    /// Decodes Base58 (Solana/Bitcoin alphabet) chars into exactly 64 bytes and enforces canonical encoding.
    /// </summary>
    public static bool TryDecodeBase58To64Chars(ReadOnlySpan<char> base58Chars, Span<byte> destination64)
    {
        if (destination64.Length != 64)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(destination64), 64);

        destination64.Clear();
        if (base58Chars.IsEmpty) return false;

        ReadOnlySpan<byte> map = Base58ReverseMap;

        int leadingOnes = 0;
        while (leadingOnes < base58Chars.Length && base58Chars[leadingOnes] == '1')
            leadingOnes++;

        if (leadingOnes > 64)
            return false;

        for (int i = leadingOnes; i < base58Chars.Length; i++)
        {
            char c = base58Chars[i];
            if (c > 255) return false;

            int digit = map[(byte)c];
            if (digit == 0xFF) return false;

            int carry = digit;
            for (int j = 63; j >= 0; j--)
            {
                int acc = destination64[j] * 58 + carry;
                destination64[j] = (byte)acc;
                carry = acc >> 8;
            }

            if (carry != 0)
                return false;
        }

        int leadingZeroBytes = 0;
        while (leadingZeroBytes < 64 && destination64[leadingZeroBytes] == 0)
            leadingZeroBytes++;

        return leadingOnes == leadingZeroBytes;
    }

    /// <summary>
    /// Attempts to normalise a JSON-RPC-like token containing a hex quantity into raw hex digits.
    /// Handles surrounding ASCII whitespace, optional JSON quotes, optional leading sign, and optional 0x prefix.
    /// Treats an empty digit sequence as zero.
    /// </summary>
    /// <param name="utf8Token">The input token (may be <c>null</c>, quoted, or unquoted).</param>
    /// <param name="isNull">True if the token is the literal <c>null</c>.</param>
    /// <param name="isNegative">True if a leading '-' sign was present.</param>
    /// <param name="hexDigitsUtf8">The normalised hex digits (no prefix, no sign). May be empty to represent zero.</param>
    /// <returns>True if token is syntactically valid; otherwise false.</returns>
    public static bool TryNormaliseHexTokenUtf8(
        ReadOnlySpan<byte> utf8Token,
        out bool isNull,
        out bool isNegative,
        out ReadOnlySpan<byte> hexDigitsUtf8)
    {
        utf8Token = TrimAsciiWhitespaceUtf8(utf8Token);

        isNull = false;
        isNegative = false;
        hexDigitsUtf8 = default;

        if (utf8Token.IsEmpty)
            return false;

        if (utf8Token.SequenceEqual("null"u8))
        {
            isNull = true;
            hexDigitsUtf8 = ReadOnlySpan<byte>.Empty;
            return true;
        }

        // Optional quotes.
        utf8Token = UnquoteJsonStringUtf8(utf8Token);
        utf8Token = TrimAsciiWhitespaceUtf8(utf8Token);

        if (!TryTrimLeadingSignUtf8(utf8Token, out isNegative, out var unsignedUtf8))
            return false;

        unsignedUtf8 = TrimAsciiWhitespaceUtf8(unsignedUtf8);

        if (unsignedUtf8.IsEmpty)
        {
            hexDigitsUtf8 = ReadOnlySpan<byte>.Empty;
            return true; // "-" or "" treated as zero after normalisation; caller may reject if desired.
        }

        if (TryTrimHexPrefixUtf8(unsignedUtf8, out var digitsAfterPrefix))
            unsignedUtf8 = digitsAfterPrefix;

        // Empty after 0x => zero.
        if (unsignedUtf8.IsEmpty)
        {
            hexDigitsUtf8 = ReadOnlySpan<byte>.Empty;
            return true;
        }

        // Require valid hex digits.
        if (!IsAllHexUtf8(unsignedUtf8))
            return false;

        hexDigitsUtf8 = unsignedUtf8;
        return true;
    }

    /// <summary>
    /// Attempts to parse an unsigned hexadecimal digit sequence into a <see cref="BigInteger"/> without allocating
    /// intermediate buffers (stackalloc for small values; pooled buffers for larger values).
    /// </summary>
    /// <param name="hexDigitsUtf8">Hex digits in UTF-8 with no prefix and no sign.</param>
    /// <param name="maxHexDigits">Maximum allowed hex digit count for DoS hygiene.</param>
    /// <param name="value">The parsed value on success.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParseBigIntegerHexUnsignedUtf8(
        ReadOnlySpan<byte> hexDigitsUtf8,
        int maxHexDigits,
        out BigInteger value)
    {
        value = BigInteger.Zero;

        if (hexDigitsUtf8.IsEmpty)
            return true; // zero

        if ((uint)hexDigitsUtf8.Length > (uint)maxHexDigits)
            return false;

        int requiredBytes = (hexDigitsUtf8.Length + 1) >> 1;

        const int StackLimitBytes = 128; // good for up to 2048-bit values (256 hex digits)
        byte[]? rented = null;

        try
        {
            Span<byte> magnitudeBigEndian = requiredBytes <= StackLimitBytes
                ? stackalloc byte[requiredBytes]
                : (rented = ArrayPool<byte>.Shared.Rent(requiredBytes));

            if (!TryDecodeHexUtf8(hexDigitsUtf8, magnitudeBigEndian, out int bytesWritten, allowOddLength: true))
                return false;

            // BigInteger can consume big-endian bytes directly; this avoids reversing.
            value = new BigInteger(magnitudeBigEndian.Slice(0, bytesWritten), isUnsigned: true, isBigEndian: true);
            return true;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Attempts to write a minimal (no leading zeros) hex representation of an unsigned big-endian magnitude into UTF-8.
    /// Optionally writes a "0x" prefix.
    /// </summary>
    /// <param name="unsignedMagnitudeBigEndian">Unsigned magnitude bytes (big-endian). May contain leading zero bytes.</param>
    /// <param name="write0xPrefix">True to write "0x" prefix.</param>
    /// <param name="uppercase">True to emit A-F; otherwise a-f.</param>
    /// <param name="destinationUtf8">Destination buffer.</param>
    /// <param name="bytesWritten">Number of bytes written on success.</param>
    /// <returns>True if formatting succeeded; otherwise false.</returns>
    public static bool TryWriteHexMinimalUtf8(
        ReadOnlySpan<byte> unsignedMagnitudeBigEndian,
        bool write0xPrefix,
        bool uppercase,
        Span<byte> destinationUtf8,
        out int bytesWritten)
    {
        bytesWritten = 0;

        // Skip leading zero bytes (but keep at least one byte for zero).
        int firstNonZero = 0;
        while (firstNonZero < unsignedMagnitudeBigEndian.Length && unsignedMagnitudeBigEndian[firstNonZero] == 0)
            firstNonZero++;

        ReadOnlySpan<byte> trimmed = firstNonZero == unsignedMagnitudeBigEndian.Length
            ? new ReadOnlySpan<byte>(new byte[] { 0x00 }) // zero
            : unsignedMagnitudeBigEndian.Slice(firstNonZero);

        // Determine if the first byte produces an odd digit count (e.g., 0x0f => "f").
        byte firstByte = trimmed[0];
        bool oddDigits = (firstByte >> 4) == 0;

        int digitCount = (trimmed.Length << 1) - (oddDigits ? 1 : 0);
        int prefixCount = write0xPrefix ? 2 : 0;
        int total = prefixCount + digitCount;

        if (destinationUtf8.Length < total)
            return false;

        ReadOnlySpan<byte> alphabet = uppercase ? HexBytesUpper : HexBytesLower;

        int pos = 0;
        if (write0xPrefix)
        {
            destinationUtf8[pos++] = (byte)'0';
            destinationUtf8[pos++] = (byte)'x';
        }

        int startIndex = 0;
        if (oddDigits)
        {
            destinationUtf8[pos++] = (byte)alphabet[firstByte & 0x0F];
            startIndex = 1;
        }
        else
        {
            destinationUtf8[pos++] = (byte)alphabet[firstByte >> 4];
            destinationUtf8[pos++] = (byte)alphabet[firstByte & 0x0F];
            startIndex = 1;
        }

        for (int i = startIndex; i < trimmed.Length; i++)
        {
            byte b = trimmed[i];
            destinationUtf8[pos++] = (byte)alphabet[b >> 4];
            destinationUtf8[pos++] = (byte)alphabet[b & 0x0F];
        }

        bytesWritten = pos;
        return true;
    }

    /// <summary>
    /// Calculates the maximum UTF-8 byte count required to write a minimal hex string for a given magnitude byte count.
    /// This includes an optional sign and optional "0x" prefix.
    /// </summary>
    /// <param name="magnitudeByteCount">Number of magnitude bytes (unsigned).</param>
    /// <param name="includeSign">True if a '-' may be written.</param>
    /// <param name="include0xPrefix">True if "0x" prefix will be written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxHexUtf8LengthFromMagnitudeByteCount(int magnitudeByteCount, bool includeSign, bool include0xPrefix)
    {
        int sign = includeSign ? 1 : 0;
        int prefix = include0xPrefix ? 2 : 0;

        // Worst case is even digit count: 2 chars per byte.
        int digits = Math.Max(1, magnitudeByteCount << 1);
        return sign + prefix + digits;
    }
}

