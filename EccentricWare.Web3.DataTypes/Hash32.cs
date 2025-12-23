using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A fixed-size 32-byte hash optimised for EVM and Solana.
/// Internally stored as 4 x 64-bit unsigned limbs in big-endian limb order (u0 is the most significant limb),
/// enabling fast equality, lexicographic ordering, and low-GC hot-path parsing/formatting.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Hash32JsonConverter))]
public readonly struct Hash32 :
    IEquatable<Hash32>,
    IComparable<Hash32>,
    IComparable,
    ISpanFormattable,
    ISpanParsable<Hash32>,
    IUtf8SpanFormattable
{
    /// <summary>The size in bytes of a <see cref="Hash32"/> value (32 bytes).</summary>
    public const int ByteLength = 32;

    /// <summary>The number of hex characters required for a 32-byte hash without a prefix (64 characters).</summary>
    public const int HexLength = 64;

    // Big-endian limb layout: _u0 is most-significant for lexicographic comparisons.
    private readonly ulong _u0;
    private readonly ulong _u1;
    private readonly ulong _u2;
    private readonly ulong _u3;

    /// <summary>The all-zero hash.</summary>
    public static readonly Hash32 Zero;

    #region Constructors

    /// <summary>
    /// Creates a new hash from four 64-bit limbs in big-endian limb order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32(ulong mostSignificantU0, ulong u1, ulong u2, ulong leastSignificantU3)
    {
        _u0 = mostSignificantU0;
        _u1 = u1;
        _u2 = u2;
        _u3 = leastSignificantU3;
    }

    /// <summary>
    /// Creates a hash from a 32-byte big-endian span.
    /// </summary>
    /// <param name="bigEndianBytes">A 32-byte span whose first byte is the most significant byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32(ReadOnlySpan<byte> bigEndianBytes)
    {
        if ((uint)bigEndianBytes.Length != ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidLength(nameof(bigEndianBytes));

        _u0 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes);
        _u1 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(8));
        _u2 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(16));
        _u3 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(24));
    }

    /// <summary>
    /// Creates a hash from a 32-byte little-endian span.
    /// This is provided for interoperability with systems that serialise the bytes in reverse significance order.
    /// </summary>
    /// <param name="littleEndianBytes">A 32-byte span whose first byte is the least significant byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 FromLittleEndian(ReadOnlySpan<byte> littleEndianBytes)
    {
        if ((uint)littleEndianBytes.Length != ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidLength(nameof(littleEndianBytes));

        ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes);
        ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(8));
        ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(16));
        ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(24));
        return new Hash32(u0, u1, u2, u3);
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the hash to a destination buffer as 32 big-endian bytes.
    /// </summary>
    /// <param name="destination">Destination span that must be at least 32 bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination)
    {
        if ((uint)destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Writes the hash to a destination buffer as 32 little-endian bytes.
    /// </summary>
    /// <param name="destination">Destination span that must be at least 32 bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination)
    {
        if ((uint)destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _u3);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24), _u0);
    }

    /// <summary>
    /// Allocates and returns a new 32-byte big-endian array.
    /// Intended for cold paths; prefer <see cref="WriteBigEndian(Span{byte})"/> for hot paths.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        var bytes = new byte[ByteLength];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Allocates and returns a new 32-byte little-endian array.
    /// Intended for cold paths; prefer <see cref="WriteLittleEndian(Span{byte})"/> for hot paths.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        var bytes = new byte[ByteLength];
        WriteLittleEndian(bytes);
        return bytes;
    }

    #endregion

    #region Equality / Hashing

    /// <summary>
    /// Compares this hash to another hash for equality.
    /// Scalar comparisons are typically optimal for single-value equality checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hash32 other)
        => _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Hash32 other && Equals(other);

    /// <summary>
    /// Computes a hash code suitable for dictionary/set usage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    /// <summary>Equality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hash32 left, Hash32 right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hash32 left, Hash32 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison using big-endian limb order (most significant limb first).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Hash32 other)
    {
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        return 0;
    }

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Hash32 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Hash32)}", nameof(obj));
    }

    /// <summary>Less-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Hash32 left, Hash32 right) => left.CompareTo(right) < 0;

    /// <summary>Greater-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Hash32 left, Hash32 right) => left.CompareTo(right) > 0;

    /// <summary>Less-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Hash32 left, Hash32 right) => left.CompareTo(right) <= 0;

    /// <summary>Greater-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Hash32 left, Hash32 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if all 32 bytes are zero.
    /// Uses a branchless OR reduction for predictable performance.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hex string (64 hex chars) with an optional 0x/0X prefix.
    /// Throws <see cref="FormatException"/> if the input is invalid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 Parse(ReadOnlySpan<char> hex)
    {
        hex = ByteUtils.TrimWhitespace(hex);
        if (ByteUtils.TryTrimHexPrefix(hex, out ReadOnlySpan<char> trimmed))
            hex = trimmed;

        if (hex.Length != HexLength)
            ThrowHelper.ThrowFormatExceptionInvalidHexLength();

        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex.Slice(0, 16), out ulong u0))
            ThrowHelper.ThrowFormatExceptionInvalidHex();
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex.Slice(16, 16), out ulong u1))
            ThrowHelper.ThrowFormatExceptionInvalidHex();
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex.Slice(32, 16), out ulong u2))
            ThrowHelper.ThrowFormatExceptionInvalidHex();
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex.Slice(48, 16), out ulong u3))
            ThrowHelper.ThrowFormatExceptionInvalidHex();

        return new Hash32(u0, u1, u2, u3);
    }

    /// <summary>
    /// Parses a UTF-8 hex string (64 hex bytes) with an optional 0x/0X prefix.
    /// Throws <see cref="FormatException"/> if the input is invalid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 Parse(ReadOnlySpan<byte> utf8)
    {
        if (TryParseHexUtf8(utf8, out var value))
            return value;

        ThrowHelper.ThrowFormatExceptionInvalidHex();
        return default;
    }

    /// <summary>
    /// Parses a Base58-encoded UTF-8 payload into a <see cref="Hash32"/>.
    /// This is the canonical Solana textual encoding for 32-byte values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 ParseBase58(ReadOnlySpan<byte> utf8)
    {
        if (TryParseBase58(utf8, out var value))
            return value;

        ThrowHelper.ThrowFormatExceptionInvalidBase58();
        return default;
    }

    /// <summary>
    /// Parses a Base64-encoded UTF-8 payload into a <see cref="Hash32"/>.
    /// Standard and URL-safe Base64 are supported. The decoded payload MUST be exactly 32 bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 ParseBase64(ReadOnlySpan<byte> utf8)
    {
        if (TryParseBase64(utf8, out var value))
            return value;

        ThrowHelper.ThrowFormatExceptionInvalidBase64();
        return default;
    }

    /// <summary>
    /// Parses a string containing a hex hash (with or without 0x prefix).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hex string (64 hex chars) with optional 0x prefix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, out Hash32 result)
    {
        result = Zero;

        s = ByteUtils.TrimWhitespace(s);
        if (ByteUtils.TryTrimHexPrefix(s, out ReadOnlySpan<char> trimmed))
            s = trimmed;

        if (s.Length != HexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt64CharsFixed16(s.Slice(0, 16), out ulong u0)) return false;
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(s.Slice(16, 16), out ulong u1)) return false;
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(s.Slice(32, 16), out ulong u2)) return false;
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(s.Slice(48, 16), out ulong u3)) return false;

        result = new Hash32(u0, u1, u2, u3);
        return true;
    }

    /// <summary>
    /// Tries to parse a nullable string containing a hex hash (with or without 0x prefix).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? s, out Hash32 result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = Zero;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 payload that is explicitly hex (optional 0x prefix) into a hash.
    /// Accepts exactly 64 hex bytes after trimming the prefix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUtf8(ReadOnlySpan<byte> utf8, out Hash32 result)
    {
        result = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        if (utf8.IsEmpty) return false;

        // Optional 0x/0X prefix.
        if (ByteUtils.TryTrimHexPrefixUtf8(utf8, out ReadOnlySpan<byte> hexDigits))
            utf8 = hexDigits;

        if (utf8.Length != HexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(utf8.Slice(0, 16), out ulong u0)) return false;
        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(utf8.Slice(16, 16), out ulong u1)) return false;
        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(utf8.Slice(32, 16), out ulong u2)) return false;
        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(utf8.Slice(48, 16), out ulong u3)) return false;

        result = new Hash32(u0, u1, u2, u3);
        return true;
    }

    /// <summary>
    /// Tries to parse a Base58-encoded UTF-8 payload into a hash.
    /// The decoded bytes MUST be exactly 32 bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseBase58(ReadOnlySpan<byte> utf8, out Hash32 result)
    {
        result = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        if (utf8.IsEmpty) return false;

        Span<byte> decoded32 = stackalloc byte[ByteLength];
        if (!ByteUtils.TryDecodeBase58To32(utf8, decoded32))
            return false;

        result = new Hash32(decoded32);
        return true;
    }

    /// <summary>
    /// Tries to parse a Base64-encoded UTF-8 payload (standard or URL-safe) into a hash.
    /// The decoded bytes MUST be exactly 32 bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseBase64(ReadOnlySpan<byte> utf8, out Hash32 result)
    {
        result = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        if (utf8.IsEmpty) return false;

        Span<byte> decoded32 = stackalloc byte[ByteLength];
        if (!ByteUtils.TryDecodeBase64Utf8(utf8, decoded32, out int bytesWritten))
            return false;

        if (bytesWritten != ByteLength)
            return false;

        result = new Hash32(decoded32);
        return true;
    }

    /// <summary>
    /// Tries to parse an "unknown source" UTF-8 hash string safely:
    /// - If it begins with 0x/0X: hex only.
    /// - Else if it is 64 hex chars: hex.
    /// - Else: Base58 (Solana canonical).
    /// - Else: Base64 (optional fallback).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseAuto(ReadOnlySpan<byte> utf8, out Hash32 result)
    {
        result = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        if (utf8.IsEmpty) return false;

        // Explicit EVM-style prefix => hex only.
        if (utf8.Length >= 2 && utf8[0] == (byte)'0' && ((utf8[1] | 0x20) == (byte)'x'))
            return TryParseHexUtf8(utf8, out result);

        // Heuristic: 64 ASCII chars and all hex => treat as hex.
        if (utf8.Length == HexLength && ByteUtils.IsAllHexUtf8(utf8))
            return TryParseHexUtf8(utf8, out result);

        // Canonical Solana: base58.
        if (TryParseBase58(utf8, out result))
            return true;

        // Optional fallback: base64.
        return TryParseBase64(utf8, out result);
    }

    /// <inheritdoc />
    public static Hash32 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        // Fix: avoid infinite recursion in Parse(ReadOnlySpan<char>, IFormatProvider?) by calling the char-only Parse overload.
        return Parse(s);
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Hash32 result)
    {
        _ = provider; // Hash32 parsing is invariant.
        return TryParse(s, out result);
    }

    /// <inheritdoc />
    public static Hash32 Parse([NotNullWhen(true)] string? s, IFormatProvider? provider)
    {
        _ = provider;
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Hash32 result)
    {
        _ = provider;
        return TryParse(s, out result);
    }

    #endregion

    #region Formatting

    /// <summary>Hex encoding table (lowercase).</summary>
    private static ReadOnlySpan<byte> HexLower => "0123456789abcdef"u8;

    /// <summary>Hex encoding table (uppercase).</summary>
    private static ReadOnlySpan<byte> HexUpper => "0123456789ABCDEF"u8;

    /// <summary>
    /// Returns the default string representation: lowercase hex with 0x prefix.
    /// </summary>
    public override string ToString()
        => string.Create(66, this, static (chars, value) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            value.FormatHexChars(chars.Slice(2), uppercase: false);
        });

    /// <summary>
    /// Formats the hash using:
    /// - "0x" (default): lowercase with prefix
    /// - "0X": uppercase with prefix
    /// - "x": lowercase without prefix
    /// - "X": uppercase without prefix
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        _ = formatProvider;

        ParseFormat(format, out bool includePrefix, out bool uppercase);

        int charCount = includePrefix ? 66 : 64;
        return string.Create(charCount, (this, includePrefix, uppercase), static (chars, state) =>
        {
            int offset = 0;
            if (state.includePrefix)
            {
                chars[0] = '0';
                chars[1] = 'x';
                offset = 2;
            }

            state.Item1.FormatHexChars(chars.Slice(offset), state.uppercase);
        });
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        _ = provider;

        ParseFormat(format.IsEmpty ? null : format.ToString(), out bool includePrefix, out bool uppercase);

        int required = includePrefix ? 66 : 64;
        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        int offset = 0;
        if (includePrefix)
        {
            destination[0] = '0';
            destination[1] = 'x';
            offset = 2;
        }

        FormatHexChars(destination.Slice(offset), uppercase);
        charsWritten = required;
        return true;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        _ = provider;

        ParseFormat(format.IsEmpty ? null : format.ToString(), out bool includePrefix, out bool uppercase);

        int required = includePrefix ? 66 : 64;
        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        int offset = 0;
        if (includePrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)'x';
            offset = 2;
        }

        FormatHexUtf8(utf8Destination.Slice(offset), uppercase);
        bytesWritten = required;
        return true;
    }

    /// <summary>
    /// Formats the hash as 64 hex characters into a destination char span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexChars(Span<char> destination64, bool uppercase)
    {
        ByteUtils.WriteHexUInt64(destination64.Slice(0, 16), _u0, uppercase);
        ByteUtils.WriteHexUInt64(destination64.Slice(16, 16), _u1, uppercase);
        ByteUtils.WriteHexUInt64(destination64.Slice(32, 16), _u2, uppercase);
        ByteUtils.WriteHexUInt64(destination64.Slice(48, 16), _u3, uppercase);
    }

    /// <summary>
    /// Formats the hash as 64 hex bytes into a destination UTF-8 span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexUtf8(Span<byte> destination64, bool uppercase)
    {
        ReadOnlySpan<byte> table = uppercase ? HexUpper : HexLower;

        ByteUtils.WriteHexUInt64Utf8(destination64.Slice(0, 16), _u0, table);
        ByteUtils.WriteHexUInt64Utf8(destination64.Slice(16, 16), _u1, table);
        ByteUtils.WriteHexUInt64Utf8(destination64.Slice(32, 16), _u2, table);
        ByteUtils.WriteHexUInt64Utf8(destination64.Slice(48, 16), _u3, table);
    }

    /// <summary>
    /// Parses the format string into prefix/uppercase flags using minimal branching and no span comparisons.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseFormat(string? format, out bool includePrefix, out bool uppercase)
    {
        // Default: "0x" lowercase.
        if (string.IsNullOrEmpty(format))
        {
            includePrefix = true;
            uppercase = false;
            return;
        }

        if (format.Length == 1)
        {
            char f0 = format[0];
            if (f0 == 'x')
            {
                includePrefix = false;
                uppercase = false;
                return;
            }
            if (f0 == 'X')
            {
                includePrefix = false;
                uppercase = true;
                return;
            }
        }
        else if (format.Length == 2)
        {
            if (format[0] == '0')
            {
                char f1 = format[1];
                if (f1 == 'x')
                {
                    includePrefix = true;
                    uppercase = false;
                    return;
                }
                if (f1 == 'X')
                {
                    includePrefix = true;
                    uppercase = true;
                    return;
                }
            }
        }

        throw new FormatException(nameof(format));
    }

    #endregion

    #region Bulk SIMD (rule-pack scanning)

    /// <summary>
    /// Searches a contiguous span for the first occurrence of <paramref name="needle"/>.
    /// Uses SIMD when available; falls back to scalar comparisons otherwise.
    /// </summary>
    /// <param name="haystack">The span to search.</param>
    /// <param name="needle">The value to locate.</param>
    /// <returns>The index of the first match, or -1 if not found.</returns>
    public static int IndexOf(ReadOnlySpan<Hash32> haystack, Hash32 needle)
    {
        if (haystack.IsEmpty) return -1;

        // SIMD pays off when scanning many items; we still keep a safe fallback.
        if (Vector256.IsHardwareAccelerated && haystack.Length >= 8)
        {
            ReadOnlySpan<byte> hayBytes = MemoryMarshal.AsBytes(haystack);
            ReadOnlySpan<byte> needleBytes = ByteUtils.AsReadOnlyBytes(in needle);

            Vector256<byte> needleVec = ByteUtils.ReadVector256(needleBytes);

            int count = haystack.Length;
            int offset = 0;
            for (int i = 0; i < count; i++, offset += ByteLength)
            {
                Vector256<byte> itemVec = ByteUtils.ReadVector256(hayBytes.Slice(offset, ByteLength));
                if (Vector256.EqualsAll(itemVec, needleVec))
                    return i;
            }

            return -1;
        }

        for (int i = 0; i < haystack.Length; i++)
        {
            if (haystack[i].Equals(needle))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Counts how many elements in <paramref name="haystack"/> equal <paramref name="needle"/>.
    /// Uses SIMD when available; falls back to scalar comparisons otherwise.
    /// </summary>
    public static int CountEquals(ReadOnlySpan<Hash32> haystack, Hash32 needle)
    {
        if (haystack.IsEmpty) return 0;

        int countMatches = 0;

        if (Vector256.IsHardwareAccelerated && haystack.Length >= 8)
        {
            ReadOnlySpan<byte> hayBytes = MemoryMarshal.AsBytes(haystack);
            ReadOnlySpan<byte> needleBytes = ByteUtils.AsReadOnlyBytes(in needle);

            Vector256<byte> needleVec = ByteUtils.ReadVector256(needleBytes);

            int count = haystack.Length;
            int offset = 0;
            for (int i = 0; i < count; i++, offset += ByteLength)
            {
                Vector256<byte> itemVec = ByteUtils.ReadVector256(hayBytes.Slice(offset, ByteLength));
                if (Vector256.EqualsAll(itemVec, needleVec))
                    countMatches++;
            }

            return countMatches;
        }

        for (int i = 0; i < haystack.Length; i++)
        {
            if (haystack[i].Equals(needle))
                countMatches++;
        }

        return countMatches;
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Explicit conversion from a 32-byte big-endian array.
    /// </summary>
    public static explicit operator Hash32(byte[] bytes) => new(bytes);

    /// <summary>
    /// Converts this hash to a <c>uint256</c> for numeric-style operations if required.
    /// This is a cold-path convenience API; the hash is not inherently numeric.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256 ToUInt256() => new(_u3, _u2, _u1, _u0);

    /// <summary>
    /// Creates a hash from a <c>uint256</c> by serialising it as big-endian bytes and re-reading.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 FromUInt256(uint256 value)
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        value.WriteBigEndian(bytes);
        return new Hash32(bytes);
    }

    #endregion
}