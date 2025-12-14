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
/// A 32-byte (256-bit) hash optimized for EVM and Solana blockchain operations.
/// Uses 4 x 64-bit unsigned integers for minimal memory footprint (32 bytes).
/// Immutable, equatable, and comparable for use as dictionary keys and sorting.
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
    /// <summary>
    /// The size in bytes of a Hash32 value (32 bytes / 256 bits).
    /// </summary>
    public const int ByteLength = 32;

    /// <summary>
    /// The size in characters of a hex string without prefix.
    /// </summary>
    public const int HexLength = 64;

    // Store as 4 x ulong (big-endian layout: _u0 is most significant for lexicographic comparison)
    private readonly ulong _u0; // bytes 0-7 (most significant)
    private readonly ulong _u1; // bytes 8-15
    private readonly ulong _u2; // bytes 16-23
    private readonly ulong _u3; // bytes 24-31 (least significant)

    /// <summary>
    /// The zero hash (all bytes are 0x00).
    /// </summary>
    public static readonly Hash32 Zero;

    #region Constructors

    /// <summary>
    /// Creates a Hash32 from 4 ulong values in big-endian order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    /// <summary>
    /// Creates a Hash32 from a 32-byte big-endian span.
    /// Compatible with EVM transaction/block hashes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidLength(nameof(bytes));

        _u0 = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        _u1 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
        _u2 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16));
        _u3 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24));
    }

    /// <summary>
    /// Creates a Hash32 from a little-endian byte span.
    /// Compatible with Solana encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 FromLittleEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidLength(nameof(bytes));

        ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8));
        ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16));
        ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(24));
        return new Hash32(u0, u1, u2, u3);
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the hash as a 32-byte big-endian span.
    /// Compatible with EVM encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Writes the hash as a 32-byte little-endian span.
    /// Compatible with Solana encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _u3);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24), _u0);
    }

    /// <summary>
    /// Returns the hash as a 32-byte big-endian array.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        var bytes = new byte[ByteLength];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the hash as a 32-byte little-endian array.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        var bytes = new byte[ByteLength];
        WriteLittleEndian(bytes);
        return bytes;
    }

    #endregion

    #region Equality (SIMD Optimized)

    /// <summary>
    /// Compares this hash for equality with another.
    /// Uses SIMD when available for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hash32 other)
    {
        // SIMD path for 256-bit comparison
        if (Vector256.IsHardwareAccelerated)
        {
            var left = Vector256.Create(_u0, _u1, _u2, _u3);
            var right = Vector256.Create(other._u0, other._u1, other._u2, other._u3);
            return left.Equals(right);
        }

        // Fallback: scalar comparison
        return _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;
    }

    public override bool Equals(object? obj) => obj is Hash32 other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hash32 left, Hash32 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hash32 left, Hash32 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison (most significant to least significant bytes).
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

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Hash32 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Hash32)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Hash32 left, Hash32 right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Hash32 left, Hash32 right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Hash32 left, Hash32 right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Hash32 left, Hash32 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a 64-character hexadecimal string (with or without 0x prefix).
    /// Uses direct nibble parsing for maximum performance.
    /// </summary>
    public static Hash32 Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != HexLength)
            ThrowHelper.ThrowFormatExceptionInvalidHexLength();

        ulong u0 = ParseHexUInt64(hex.Slice(0, 16));
        ulong u1 = ParseHexUInt64(hex.Slice(16, 16));
        ulong u2 = ParseHexUInt64(hex.Slice(32, 16));
        ulong u3 = ParseHexUInt64(hex.Slice(48, 16));

        return new Hash32(u0, u1, u2, u3);
    }

    public static Hash32 Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    public static Hash32 Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    public static Hash32 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hexadecimal string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out Hash32 result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != HexLength)
            return false;

        if (!TryParseHexUInt64(hex.Slice(0, 16), out ulong u0))
            return false;
        if (!TryParseHexUInt64(hex.Slice(16, 16), out ulong u1))
            return false;
        if (!TryParseHexUInt64(hex.Slice(32, 16), out ulong u2))
            return false;
        if (!TryParseHexUInt64(hex.Slice(48, 16), out ulong u3))
            return false;

        result = new Hash32(u0, u1, u2, u3);
        return true;
    }

    #region Hex Parsing Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexNibble(char c)
    {
        // Branchless hex nibble parsing
        int val = c;
        int digit = val - '0';
        int lower = (val | 0x20) - 'a' + 10; // Case-insensitive a-f
        
        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ParseHexUInt64(ReadOnlySpan<char> hex)
    {
        ulong result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            result = (result << 4) | (uint)nibble;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexUInt64(ReadOnlySpan<char> hex, out ulong result)
    {
        result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) return false;
            result = (result << 4) | (uint)nibble;
        }
        return true;
    }

    #endregion

    public static bool TryParse(string? hex, out Hash32 result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Hash32 result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Hash32 result)
        => TryParse(s, out result);

    #endregion

    #region Formatting

    // Lookup table for hex encoding (lowercase)
    private static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    /// <summary>
    /// Returns the hexadecimal representation with 0x prefix (lowercase).
    /// </summary>
    public override string ToString()
    {
        return string.Create(66, this, static (chars, hash) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            hash.FormatHexCore(chars.Slice(2), uppercase: false);
        });
    }

    /// <summary>
    /// Formats the value according to the format string.
    /// "x" for lowercase hex, "X" for uppercase hex, "0x" for lowercase with prefix (default), "0X" for uppercase with prefix.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        return format switch
        {
            "0x" => ToString(),
            "0X" => string.Create(66, this, static (chars, hash) =>
            {
                chars[0] = '0';
                chars[1] = 'x';
                hash.FormatHexCore(chars.Slice(2), uppercase: true);
            }),
            "x" => string.Create(64, this, static (chars, hash) =>
            {
                hash.FormatHexCore(chars, uppercase: false);
            }),
            "X" => string.Create(64, this, static (chars, hash) =>
            {
                hash.FormatHexCore(chars, uppercase: true);
            }),
            _ => throw new FormatException($"Unknown format: {format}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexCore(Span<char> destination, bool uppercase)
    {
        FormatUInt64Hex(_u0, destination, uppercase);
        FormatUInt64Hex(_u1, destination.Slice(16), uppercase);
        FormatUInt64Hex(_u2, destination.Slice(32), uppercase);
        FormatUInt64Hex(_u3, destination.Slice(48), uppercase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt64Hex(ulong value, Span<char> destination, bool uppercase)
    {
        // Format 16 hex characters from a ulong
        for (int i = 15; i >= 0; i--)
        {
            int nibble = (int)(value & 0xF);
            destination[i] = (char)(uppercase ? HexBytesUpper[nibble] : HexBytesLower[nibble]);
            value >>= 4;
        }
    }

    /// <summary>
    /// Tries to format the value into the destination span.
    /// Zero-allocation formatting.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? 66 : 64;

        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            destination[0] = '0';
            destination[1] = 'x';
            FormatHexCore(destination.Slice(2), uppercase);
        }
        else
        {
            FormatHexCore(destination, uppercase);
        }

        charsWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Tries to format the value into a UTF-8 destination span.
    /// Zero-allocation formatting for JSON serialization.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? 66 : 64;

        if (utf8Destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)'x';
            FormatHexCoreUtf8(utf8Destination.Slice(2), uppercase);
        }
        else
        {
            FormatHexCoreUtf8(utf8Destination, uppercase);
        }

        bytesWritten = requiredLength;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexCoreUtf8(Span<byte> destination, bool uppercase)
    {
        FormatUInt64HexUtf8(_u0, destination, uppercase);
        FormatUInt64HexUtf8(_u1, destination.Slice(16), uppercase);
        FormatUInt64HexUtf8(_u2, destination.Slice(32), uppercase);
        FormatUInt64HexUtf8(_u3, destination.Slice(48), uppercase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt64HexUtf8(ulong value, Span<byte> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        for (int i = 15; i >= 0; i--)
        {
            destination[i] = hexTable[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if this is the zero hash.
    /// Branchless implementation for better CPU pipeline efficiency.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Explicit conversion from byte array.
    /// </summary>
    public static explicit operator Hash32(byte[] bytes) => new(bytes);

    /// <summary>
    /// Converts to uint256 for numeric operations if needed.
    /// Zero-allocation direct conversion.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256 ToUInt256() => new(_u3, _u2, _u1, _u0);

    /// <summary>
    /// Creates a Hash32 from a uint256.
    /// Zero-allocation direct conversion.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 FromUInt256(uint256 value)
    {
        // uint256 stores as little-endian internally: _u0 is LSB, _u3 is MSB
        // Hash32 stores as big-endian: _u0 is MSB
        // Direct field access via WriteBigEndian then read back
        Span<byte> bytes = stackalloc byte[ByteLength];
        value.WriteBigEndian(bytes);
        return new Hash32(bytes);
    }

    #endregion
}
