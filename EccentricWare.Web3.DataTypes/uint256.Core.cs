using EccentricWare.Web3.DataTypes.Utils;
using EccentricWare.Web3.DataTypes.Utils.Uint256;
using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// Fixed-size 256-bit unsigned integer optimised for indexing, JSON-RPC parsing, and base-unit coin arithmetic.
/// </summary>
/// <remarks>
/// Metadata tags: [hotpath] [indexing] [json] [erc20] [no-gc]
///
/// Formatting rules:
/// - Default format (empty / null) is EVM quantity: "0x" + minimal hex (lowercase), and "0x0" for zero.
/// - "x" / "X": minimal hex without prefix.
/// - "0x" / "0X": minimal hex with prefix.
/// - "x64" / "X64": fixed-width 64 hex digits without prefix.
/// - "0x64" / "0X64": fixed-width 64 hex digits with prefix.
/// - "D" / "d": decimal (slow-path).
///
/// Parsing rules (ISpanParsable):
/// - If prefixed with 0x/0X: hex (1..64 digits), leading zeros allowed.
/// - Otherwise: unsigned decimal digits.
/// - ASCII whitespace is trimmed.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct uint256 :
    IEquatable<uint256>,
    IComparable<uint256>,
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<uint256>
{
    // Little-endian limbs.
    private readonly ulong _u0;
    private readonly ulong _u1;
    private readonly ulong _u2;
    private readonly ulong _u3;

    /// <summary>Least significant 64 bits.</summary>
    public ulong Limb0 => _u0;

    /// <summary>Bits 64..127.</summary>
    public ulong Limb1 => _u1;

    /// <summary>Bits 128..191.</summary>
    public ulong Limb2 => _u2;

    /// <summary>Most significant 64 bits.</summary>
    public ulong Limb3 => _u3;

    /// <summary>Represents 0.</summary>
    public static readonly uint256 Zero = new uint256(0);

    /// <summary>Represents 1.</summary>
    public static readonly uint256 One = new uint256(1UL);

    /// <summary>Maximum representable value (2^256 - 1).</summary>
    public static readonly uint256 MaxValue = new uint256(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    /// <summary>
    /// Creates a value from a 64-bit unsigned integer.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong value)
    {
        _u0 = value;
        _u1 = 0;
        _u2 = 0;
        _u3 = 0;
    }

    /// <summary>
    /// Creates a value from four 64-bit limbs (little-endian).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong limb0, ulong limb1, ulong limb2, ulong limb3)
    {
        _u0 = limb0;
        _u1 = limb1;
        _u2 = limb2;
        _u3 = limb3;
    }

    /// <summary>
    /// True if the value equals zero.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath]</remarks>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    /// <summary>
    /// Writes the value into a 32-byte big-endian destination span (ABI/key canonical form).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [abi] [bytes32]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination32)
    {
        if (destination32.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination32));

        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(0, 8), _u3);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(8, 8), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(16, 8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(24, 8), _u0);
    }

    /// <summary>
    /// Writes the value into a 32-byte little-endian destination span (Solana/native).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [solana] [bytes]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination32)
    {
        if (destination32.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination32));

        BinaryPrimitives.WriteUInt64LittleEndian(destination32.Slice(0, 8), _u0);
        BinaryPrimitives.WriteUInt64LittleEndian(destination32.Slice(8, 8), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination32.Slice(16, 8), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination32.Slice(24, 8), _u3);
    }

    /// <summary>
    /// Creates a value from a 32-byte big-endian span.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [abi] [bytes32]</remarks>
    public static uint256 FromBigEndian32(ReadOnlySpan<byte> source32BigEndian)
    {
        if (source32BigEndian.Length != 32)
            throw new ArgumentException("Expected exactly 32 bytes.", nameof(source32BigEndian));

        ulong u3 = BinaryPrimitives.ReadUInt64BigEndian(source32BigEndian.Slice(0, 8));
        ulong u2 = BinaryPrimitives.ReadUInt64BigEndian(source32BigEndian.Slice(8, 8));
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(source32BigEndian.Slice(16, 8));
        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(source32BigEndian.Slice(24, 8));

        return new uint256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Parses a UTF-8 value according to a strict mode suitable for firewall enforcement.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [json] [firewall] [canonicalisation]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> utf8Value, UInt256ParseMode parseMode, out uint256 parsedValue)
        => UInt256Parser.TryParseUtf8(utf8Value, parseMode, out parsedValue);

    /// <summary>
    /// Tries to read a JSON-RPC numeric value from a <see cref="Utf8JsonReader"/> (hot-path friendly).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [json] [firewall]</remarks>
    public static bool TryReadJsonRpcValue(ref Utf8JsonReader jsonReader, UInt256ParseMode parseMode, out uint256 parsedValue)
        => UInt256Parser.TryReadJsonRpcValue(ref jsonReader, parseMode, out parsedValue);

    // --------------------------
    // Interface implementations
    // --------------------------

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(uint256 other)
        => (((_u0 ^ other._u0) | (_u1 ^ other._u1) | (_u2 ^ other._u2) | (_u3 ^ other._u3)) == 0);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is uint256 other && Equals(other);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(uint256 other)
    {
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        return 0;
    }

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is uint256 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(uint256)}.", nameof(obj));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        ulong h = GetStableHash64();
        return (int)(h ^ (h >> 32));
    }

    /// <inheritdoc />
    public static bool operator ==(uint256 left, uint256 right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(uint256 left, uint256 right) => !left.Equals(right);

    /// <inheritdoc />
    public static bool operator <(uint256 left, uint256 right) => left.CompareTo(right) < 0;

    /// <inheritdoc />
    public static bool operator >(uint256 left, uint256 right) => left.CompareTo(right) > 0;

    /// <inheritdoc />
    public static bool operator <=(uint256 left, uint256 right) => left.CompareTo(right) <= 0;

    /// <inheritdoc />
    public static bool operator >=(uint256 left, uint256 right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Returns a stable 64-bit hash suitable for indexing and sharding.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [indexing] [stable-hash]</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetStableHash64()
    {
        static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        ulong h = Mix(_u0);
        h = Mix(h ^ _u1);
        h = Mix(h ^ _u2);
        h = Mix(h ^ _u3);
        return h;
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        // Allocate only here. Hot-path should use TryFormat overloads.
        format ??= string.Empty;

        if (format.Length == 1 && (format[0] == 'D' || format[0] == 'd'))
        {
            // Slow-path decimal.
            return ToBigInteger().ToString(formatProvider);
        }

        Span<char> tmp = stackalloc char[66]; // max "0x" + 64 digits
        if (!TryFormat(tmp, out int charsWritten, format.AsSpan(), formatProvider))
            throw new FormatException("Destination buffer too small (unexpected).");

        return new string(tmp.Slice(0, charsWritten));
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (!TryParseFormat(format, out var spec))
        {
            charsWritten = 0;
            return false;
        }

        if (spec.Kind == UInt256FormatKind.Decimal)
        {
            // Slow-path decimal formatting (BigInteger), but allocation-free with TryFormat.
            return ToBigInteger().TryFormat(destination, out charsWritten, default, provider);
        }

        if (spec.Kind == UInt256FormatKind.HexFixed64)
            return TryWriteHexFixed64Chars(destination, spec.WithPrefix, spec.Uppercase, spec.PrefixChar, out charsWritten);

        // HexMinimal / EVM quantity
        return TryWriteHexMinimalChars(destination, spec.WithPrefix, spec.Uppercase, spec.PrefixChar, out charsWritten);
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (!TryParseFormat(format, out var spec))
        {
            bytesWritten = 0;
            return false;
        }

        if (spec.Kind == UInt256FormatKind.Decimal)
        {
            // Slow-path: decimal digits are ASCII; format into chars then copy.
            Span<char> tmp = stackalloc char[80]; // enough for 78 digits
            if (!ToBigInteger().TryFormat(tmp, out int writtenChars, default, provider))
            {
                bytesWritten = 0;
                return false;
            }

            if (utf8Destination.Length < writtenChars)
            {
                bytesWritten = 0;
                return false;
            }

            for (int i = 0; i < writtenChars; i++)
                utf8Destination[i] = (byte)tmp[i];

            bytesWritten = writtenChars;
            return true;
        }

        if (spec.Kind == UInt256FormatKind.HexFixed64)
            return TryWriteHexFixed64Utf8(utf8Destination, spec.WithPrefix, spec.Uppercase, spec.PrefixChar, out bytesWritten);

        // HexMinimal / EVM quantity
        return TryWriteHexMinimalUtf8(utf8Destination, spec.WithPrefix, spec.Uppercase, spec.PrefixChar, out bytesWritten);
    }

    /// <inheritdoc />
    public static uint256 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var value))
            return value;

        throw new FormatException("Invalid uint256 value.");
    }

    /// <inheritdoc />
    public static uint256 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out uint256 result)
    {
        result = Zero;

        ReadOnlySpan<char> text = ByteUtils.TrimAsciiWhitespace(s);
        if (text.Length == 0) return false;

        if (ByteUtils.Has0xPrefix(text))
        {
            text = text.Slice(2);
            if (text.Length == 0 || text.Length > 64) return false;
            return TryParseHexNibblesUtf16(text, out result);
        }

        return TryParseDecimalUtf16(text, out result);
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out uint256 result)
    {
        result = default;
        return s is not null && TryParse(s.AsSpan(), provider, out result);
    }

    // --------------------------
    // Formatting internals
    // --------------------------

    private enum UInt256FormatKind : byte
    {
        HexMinimal = 0,
        HexFixed64 = 1,
        Decimal = 2
    }

    private readonly struct UInt256FormatSpec
    {
        public readonly UInt256FormatKind Kind;
        public readonly bool WithPrefix;
        public readonly bool Uppercase;
        public readonly char PrefixChar;

        public UInt256FormatSpec(UInt256FormatKind kind, bool withPrefix, bool uppercase, char prefixChar)
        {
            Kind = kind;
            WithPrefix = withPrefix;
            Uppercase = uppercase;
            PrefixChar = prefixChar;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseFormat(ReadOnlySpan<char> format, out UInt256FormatSpec spec)
    {
        // Default: EVM quantity lower: 0x + minimal lower.
        if (format.Length == 0)
        {
            spec = new UInt256FormatSpec(UInt256FormatKind.HexMinimal, withPrefix: true, uppercase: false, prefixChar: 'x');
            return true;
        }

        if (format.Length == 1)
        {
            char f = format[0];
            if (f == 'x') { spec = new UInt256FormatSpec(UInt256FormatKind.HexMinimal, false, false, 'x'); return true; }
            if (f == 'X') { spec = new UInt256FormatSpec(UInt256FormatKind.HexMinimal, false, true, 'X'); return true; }
            if (f == 'd' || f == 'D') { spec = new UInt256FormatSpec(UInt256FormatKind.Decimal, false, false, 'x'); return true; }
            spec = default;
            return false;
        }

        if (format.Length == 2)
        {
            if (format[0] == '0' && (format[1] == 'x' || format[1] == 'X'))
            {
                bool upper = format[1] == 'X';
                spec = new UInt256FormatSpec(UInt256FormatKind.HexMinimal, withPrefix: true, uppercase: upper, prefixChar: format[1]);
                return true;
            }
            spec = default;
            return false;
        }

        // Fixed-width 64: "x64"/"X64" or "0x64"/"0X64"
        if (format.Length == 3)
        {
            if ((format[0] == 'x' || format[0] == 'X') && format[1] == '6' && format[2] == '4')
            {
                bool upper = format[0] == 'X';
                spec = new UInt256FormatSpec(UInt256FormatKind.HexFixed64, withPrefix: false, uppercase: upper, prefixChar: format[0]);
                return true;
            }

            spec = default;
            return false;
        }

        if (format.Length == 4)
        {
            if (format[0] == '0' && (format[1] == 'x' || format[1] == 'X') && format[2] == '6' && format[3] == '4')
            {
                bool upper = format[1] == 'X';
                spec = new UInt256FormatSpec(UInt256FormatKind.HexFixed64, withPrefix: true, uppercase: upper, prefixChar: format[1]);
                return true;
            }

            spec = default;
            return false;
        }

        spec = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryWriteHexMinimalUtf8(Span<byte> destinationUtf8, bool withPrefix, bool uppercase, char prefixChar, out int bytesWritten)
    {
        // Max is 66: "0x" + 64 digits.
        bytesWritten = 0;
        int pos = 0;

        if (withPrefix)
        {
            if (destinationUtf8.Length < 2) return false;
            destinationUtf8[pos++] = (byte)'0';
            destinationUtf8[pos++] = (byte)prefixChar; // 'x' or 'X'
        }

        if (IsZero)
        {
            if (destinationUtf8.Length < pos + 1) return false;
            destinationUtf8[pos++] = (byte)'0';
            bytesWritten = pos;
            return true;
        }

        // Find highest non-zero limb.
        if (_u3 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u3, destinationUtf8.Slice(pos), uppercase, out int w3)) return false;
            pos += w3;
            if (destinationUtf8.Length - pos < 48) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u2, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u1, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            bytesWritten = pos;
            return true;
        }

        if (_u2 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u2, destinationUtf8.Slice(pos), uppercase, out int w2)) return false;
            pos += w2;
            if (destinationUtf8.Length - pos < 32) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u1, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            bytesWritten = pos;
            return true;
        }

        if (_u1 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u1, destinationUtf8.Slice(pos), uppercase, out int w1)) return false;
            pos += w1;
            if (destinationUtf8.Length - pos < 16) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
            bytesWritten = pos;
            return true;
        }

        if (!ByteUtils.TryWriteHexUInt64Minimal(_u0, destinationUtf8.Slice(pos), uppercase, out int w0)) return false;
        pos += w0;
        bytesWritten = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryWriteHexFixed64Utf8(Span<byte> destinationUtf8, bool withPrefix, bool uppercase, char prefixChar, out int bytesWritten)
    {
        bytesWritten = 0;
        int required = withPrefix ? 66 : 64;
        if (destinationUtf8.Length < required) return false;

        int pos = 0;
        if (withPrefix)
        {
            destinationUtf8[pos++] = (byte)'0';
            destinationUtf8[pos++] = (byte)prefixChar;
        }

        ByteUtils.WriteHexUInt64Fixed16(_u3, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u2, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u1, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u0, destinationUtf8.Slice(pos, 16), uppercase); pos += 16;

        bytesWritten = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryWriteHexMinimalChars(Span<char> destination, bool withPrefix, bool uppercase, char prefixChar, out int charsWritten)
    {
        charsWritten = 0;
        int pos = 0;

        if (withPrefix)
        {
            if (destination.Length < 2) return false;
            destination[pos++] = '0';
            destination[pos++] = prefixChar; // 'x' or 'X'
        }

        if (IsZero)
        {
            if (destination.Length < pos + 1) return false;
            destination[pos++] = '0';
            charsWritten = pos;
            return true;
        }

        if (_u3 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u3, destination.Slice(pos), uppercase, out int w3)) return false;
            pos += w3;
            if (destination.Length - pos < 48) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u2, destination.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u1, destination.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destination.Slice(pos, 16), uppercase); pos += 16;
            charsWritten = pos;
            return true;
        }

        if (_u2 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u2, destination.Slice(pos), uppercase, out int w2)) return false;
            pos += w2;
            if (destination.Length - pos < 32) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u1, destination.Slice(pos, 16), uppercase); pos += 16;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destination.Slice(pos, 16), uppercase); pos += 16;
            charsWritten = pos;
            return true;
        }

        if (_u1 != 0)
        {
            if (!ByteUtils.TryWriteHexUInt64Minimal(_u1, destination.Slice(pos), uppercase, out int w1)) return false;
            pos += w1;
            if (destination.Length - pos < 16) return false;
            ByteUtils.WriteHexUInt64Fixed16(_u0, destination.Slice(pos, 16), uppercase); pos += 16;
            charsWritten = pos;
            return true;
        }

        if (!ByteUtils.TryWriteHexUInt64Minimal(_u0, destination.Slice(pos), uppercase, out int w0)) return false;
        pos += w0;
        charsWritten = pos;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryWriteHexFixed64Chars(Span<char> destination, bool withPrefix, bool uppercase, char prefixChar, out int charsWritten)
    {
        charsWritten = 0;
        int required = withPrefix ? 66 : 64;
        if (destination.Length < required) return false;

        int pos = 0;
        if (withPrefix)
        {
            destination[pos++] = '0';
            destination[pos++] = prefixChar;
        }

        ByteUtils.WriteHexUInt64Fixed16(_u3, destination.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u2, destination.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u1, destination.Slice(pos, 16), uppercase); pos += 16;
        ByteUtils.WriteHexUInt64Fixed16(_u0, destination.Slice(pos, 16), uppercase); pos += 16;

        charsWritten = pos;
        return true;
    }

    // --------------------------
    // Parsing internals (UTF-16)
    // --------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexNibblesUtf16(ReadOnlySpan<char> hexNibbles, out uint256 value)
    {
        value = Zero;
        if ((uint)hexNibbles.Length > 64) return false;

        // Parse from rightmost digits into limbs.
        ulong u0 = 0, u1 = 0, u2 = 0, u3 = 0;
        int endExclusive = hexNibbles.Length;

        int start = endExclusive > 16 ? endExclusive - 16 : 0;
        if (!ByteUtils.TryParseHexUInt64Utf16Variable(hexNibbles.Slice(start, endExclusive - start), out u0)) return false;
        endExclusive = start;

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf16Variable(hexNibbles.Slice(start, endExclusive - start), out u1)) return false;
            endExclusive = start;
        }

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf16Variable(hexNibbles.Slice(start, endExclusive - start), out u2)) return false;
            endExclusive = start;
        }

        if (endExclusive > 0)
        {
            start = endExclusive > 16 ? endExclusive - 16 : 0;
            if (!ByteUtils.TryParseHexUInt64Utf16Variable(hexNibbles.Slice(start, endExclusive - start), out u3)) return false;
        }

        value = new uint256(u0, u1, u2, u3);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseDecimalUtf16(ReadOnlySpan<char> digits, out uint256 value)
    {
        value = Zero;
        if (digits.Length == 0) return false;

        uint256 acc = Zero;

        for (int i = 0; i < digits.Length; i++)
        {
            char c = digits[i];
            int d = c - '0';
            if ((uint)d > 9) return false;

            if (!UInt256Math.TryMul10AddDigit(ref acc, (byte)d))
                return false; // overflow
        }

        value = acc;
        return true;
    }

    // --------------------------
    // Existing hot-path ops
    // --------------------------

    /// <summary>
    /// Scales the value up by 10^decimals (typical for converting token units to base units), with a fast-path for decimals 0..19.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [erc20] [pow10]</remarks>
    public bool TryScaleUpPow10(byte decimalPlaces, out uint256 scaledValue)
    {
        if (UInt256Pow10.TryGetPow10U64(decimalPlaces, out ulong pow10))
        {
            scaledValue = UInt256Math.MulU64(this, pow10, out ulong carryOut);
            return carryOut == 0;
        }

        // Explicit slow-path hook (BigInteger).
        return TryScaleUpPow10Slow(decimalPlaces, out scaledValue);
    }

    /// <summary>
    /// Scales the value down by 10^decimals (fast-path only for 0..19), returning remainder.
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [erc20] [pow10]</remarks>
    public uint256 ScaleDownPow10Fast(byte decimalPlaces, out ulong remainder)
    {
        if (!UInt256Pow10.TryGetPow10U64(decimalPlaces, out ulong pow10))
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Fast-path supports decimalPlaces 0..19 only.");

        return UInt256Math.DivRemU64(this, pow10, out remainder);
    }

    /// <summary>
    /// Left shift (correct for all shift values; shifts ≥ 256 yield zero).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [bitops]</remarks>
    public static uint256 operator <<(uint256 value, int shift) => ShiftLeft(value, shift);

    /// <summary>
    /// Right shift (correct for all shift values; shifts ≥ 256 yield zero).
    /// </summary>
    /// <remarks>Metadata tags: [hotpath] [bitops]</remarks>
    public static uint256 operator >>(uint256 value, int shift) => ShiftRight(value, shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint256 ShiftLeft(uint256 value, int shift)
    {
        if ((uint)shift >= 256) return Zero;
        if (shift == 0) return value;

        int wordShift = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;

        if (bitShift == 0)
        {
            return wordShift switch
            {
                0 => value,
                1 => new uint256(0, a0, a1, a2),
                2 => new uint256(0, 0, a0, a1),
                3 => new uint256(0, 0, 0, a0),
                _ => Zero
            };
        }

        ulong r0, r1, r2, r3;

        switch (wordShift)
        {
            case 0:
                r0 = a0 << bitShift;
                r1 = (a1 << bitShift) | (a0 >> (64 - bitShift));
                r2 = (a2 << bitShift) | (a1 >> (64 - bitShift));
                r3 = (a3 << bitShift) | (a2 >> (64 - bitShift));
                break;

            case 1:
                r0 = 0;
                r1 = a0 << bitShift;
                r2 = (a1 << bitShift) | (a0 >> (64 - bitShift));
                r3 = (a2 << bitShift) | (a1 >> (64 - bitShift));
                break;

            case 2:
                r0 = 0; r1 = 0;
                r2 = a0 << bitShift;
                r3 = (a1 << bitShift) | (a0 >> (64 - bitShift));
                break;

            case 3:
                r0 = 0; r1 = 0; r2 = 0;
                r3 = a0 << bitShift;
                break;

            default:
                return Zero;
        }

        return new uint256(r0, r1, r2, r3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint256 ShiftRight(uint256 value, int shift)
    {
        if ((uint)shift >= 256) return Zero;
        if (shift == 0) return value;

        int wordShift = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;

        if (bitShift == 0)
        {
            return wordShift switch
            {
                0 => value,
                1 => new uint256(a1, a2, a3, 0),
                2 => new uint256(a2, a3, 0, 0),
                3 => new uint256(a3, 0, 0, 0),
                _ => Zero
            };
        }

        ulong r0, r1, r2, r3;

        switch (wordShift)
        {
            case 0:
                r3 = a3 >> bitShift;
                r2 = (a2 >> bitShift) | (a3 << (64 - bitShift));
                r1 = (a1 >> bitShift) | (a2 << (64 - bitShift));
                r0 = (a0 >> bitShift) | (a1 << (64 - bitShift));
                break;

            case 1:
                r3 = 0;
                r2 = a3 >> bitShift;
                r1 = (a2 >> bitShift) | (a3 << (64 - bitShift));
                r0 = (a1 >> bitShift) | (a2 << (64 - bitShift));
                break;

            case 2:
                r3 = 0; r2 = 0;
                r1 = a3 >> bitShift;
                r0 = (a2 >> bitShift) | (a3 << (64 - bitShift));
                break;

            case 3:
                r3 = 0; r2 = 0; r1 = 0;
                r0 = a3 >> bitShift;
                break;

            default:
                return Zero;
        }

        return new uint256(r0, r1, r2, r3);
    }


}
