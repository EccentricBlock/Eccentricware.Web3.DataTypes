using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// An immutable variable-length byte array with hex string encoding.
/// Optimized for blockchain calldata, event data, and RPC responses.
/// 
/// This is a lightweight wrapper around a byte array that provides
/// efficient hex encoding/decoding and equality comparison.
/// 
/// For fixed-size data, prefer Hash32, Address, or FunctionSelector.
/// </summary>
[JsonConverter(typeof(HexBytesJsonConverter))]
public readonly struct HexBytes : 
    IEquatable<HexBytes>, 
    IComparable<HexBytes>, 
    IComparable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    // Immutable backing array (null represents empty)
    private readonly byte[]? _bytes;

    /// <summary>
    /// Empty byte array.
    /// </summary>
    public static readonly HexBytes Empty;

    // Hex lookup tables
    private static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    #region Constructors

    /// <summary>
    /// Creates HexBytes from a byte array. Makes a defensive copy.
    /// </summary>
    public HexBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            _bytes = null;
        }
        else
        {
            _bytes = new byte[bytes.Length];
            bytes.CopyTo(_bytes, 0);
        }
    }

    /// <summary>
    /// Creates HexBytes from a span. Makes a copy.
    /// </summary>
    public HexBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            _bytes = null;
        }
        else
        {
            _bytes = bytes.ToArray();
        }
    }

    /// <summary>
    /// Creates HexBytes from existing array without copying.
    /// INTERNAL USE ONLY - caller must ensure array is not modified.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private HexBytes(byte[]? bytes, bool noCopy)
    {
        _bytes = bytes;
    }

    /// <summary>
    /// Creates HexBytes from existing array without copying.
    /// Caller guarantees the array will not be modified.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBytes FromArrayUnsafe(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return Empty;
        return new HexBytes(bytes, noCopy: true);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the length in bytes.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bytes?.Length ?? 0;
    }

    /// <summary>
    /// Returns true if empty.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bytes == null || _bytes.Length == 0;
    }

    /// <summary>
    /// Gets the hex string length (without prefix).
    /// </summary>
    public int HexLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Length * 2;
    }

    /// <summary>
    /// Gets a read-only span over the bytes.
    /// </summary>
    public ReadOnlySpan<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bytes ?? ReadOnlySpan<byte>.Empty;
    }

    /// <summary>
    /// Gets a read-only memory over the bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bytes ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Gets a byte at the specified index.
    /// </summary>
    public byte this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_bytes == null || (uint)index >= (uint)_bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _bytes[index];
        }
    }

    #endregion

    #region Byte Operations

    /// <summary>
    /// Returns a copy of the bytes as an array.
    /// </summary>
    public byte[] ToArray()
    {
        if (_bytes == null) return [];
        var copy = new byte[_bytes.Length];
        _bytes.CopyTo(copy, 0);
        return copy;
    }

    /// <summary>
    /// Copies bytes to destination span.
    /// </summary>
    public void CopyTo(Span<byte> destination)
    {
        if (_bytes != null)
            _bytes.CopyTo(destination);
    }

    /// <summary>
    /// Creates a slice of the bytes.
    /// </summary>
    public HexBytes Slice(int start)
    {
        if (_bytes == null) return Empty;
        return new HexBytes(_bytes.AsSpan(start));
    }

    /// <summary>
    /// Creates a slice of the bytes.
    /// </summary>
    public HexBytes Slice(int start, int length)
    {
        if (_bytes == null) return Empty;
        return new HexBytes(_bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Concatenates two HexBytes.
    /// </summary>
    public static HexBytes Concat(HexBytes a, HexBytes b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;

        var result = new byte[a.Length + b.Length];
        a.Span.CopyTo(result);
        b.Span.CopyTo(result.AsSpan(a.Length));
        return FromArrayUnsafe(result);
    }

    /// <summary>
    /// Operator for concatenation.
    /// </summary>
    public static HexBytes operator +(HexBytes a, HexBytes b) => Concat(a, b);

    #endregion

    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(HexBytes other)
    {
        return Span.SequenceEqual(other.Span);
    }

    public override bool Equals(object? obj) => obj is HexBytes other && Equals(other);

    public override int GetHashCode()
    {
        if (_bytes == null) return 0;

        // Use first 16 bytes for hash (sufficient for uniqueness)
        var span = _bytes.AsSpan();
        int len = Math.Min(span.Length, 16);
        
        var hash = new HashCode();
        hash.Add(span.Length);
        for (int i = 0; i < len; i++)
            hash.Add(span[i]);
        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(HexBytes left, HexBytes right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HexBytes left, HexBytes right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison.
    /// </summary>
    public int CompareTo(HexBytes other)
    {
        var left = Span;
        var right = other.Span;
        int minLen = Math.Min(left.Length, right.Length);

        for (int i = 0; i < minLen; i++)
        {
            if (left[i] != right[i])
                return left[i] < right[i] ? -1 : 1;
        }

        return left.Length.CompareTo(right.Length);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is HexBytes other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(HexBytes)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(HexBytes left, HexBytes right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(HexBytes left, HexBytes right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(HexBytes left, HexBytes right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(HexBytes left, HexBytes right) => left.CompareTo(right) >= 0;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hex string (with or without 0x prefix).
    /// </summary>
    public static HexBytes Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return Empty;

        if ((hex.Length & 1) != 0)
            ThrowHelper.ThrowFormatExceptionOddHexLength();

        int byteCount = hex.Length / 2;
        var bytes = new byte[byteCount];

        for (int i = 0; i < byteCount; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0)
                ThrowHelper.ThrowFormatExceptionInvalidHex();
            bytes[i] = (byte)((hi << 4) | lo);
        }

        return FromArrayUnsafe(bytes);
    }

    public static HexBytes Parse(string hex) => Parse(hex.AsSpan());

    /// <summary>
    /// Tries to parse a hex string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out HexBytes result)
    {
        result = Empty;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return true;

        if ((hex.Length & 1) != 0)
            return false;

        int byteCount = hex.Length / 2;
        var bytes = new byte[byteCount];

        for (int i = 0; i < byteCount; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0)
                return false;
            bytes[i] = (byte)((hi << 4) | lo);
        }

        result = FromArrayUnsafe(bytes);
        return true;
    }

    public static bool TryParse(string? hex, out HexBytes result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Empty;
            return true;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexNibble(char c)
    {
        int val = c;
        int digit = val - '0';
        int lower = (val | 0x20) - 'a' + 10;
        
        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the hex representation with 0x prefix.
    /// </summary>
    public override string ToString()
    {
        if (_bytes == null || _bytes.Length == 0)
            return "0x";

        return string.Create(HexLength + 2, this, static (chars, hb) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            hb.FormatHexCore(chars.Slice(2), uppercase: false);
        });
    }

    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        if (_bytes == null || _bytes.Length == 0)
        {
            bool hasPrefix = format == null || format == "0x" || format == "0X";
            return hasPrefix ? "0x" : "";
        }

        format ??= "0x";
        bool uppercase = format == "X" || format == "0X";
        bool withPrefix = format == "0x" || format == "0X" || format.Length == 0;

        int totalLen = withPrefix ? HexLength + 2 : HexLength;
        return string.Create(totalLen, (this, uppercase, withPrefix), static (chars, state) =>
        {
            int pos = 0;
            if (state.withPrefix)
            {
                chars[0] = '0';
                chars[1] = 'x';
                pos = 2;
            }
            state.Item1.FormatHexCore(chars.Slice(pos), state.uppercase);
        });
    }

    private void FormatHexCore(Span<char> destination, bool uppercase)
    {
        if (_bytes == null) return;

        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        for (int i = 0; i < _bytes.Length; i++)
        {
            byte b = _bytes[i];
            destination[i * 2] = (char)hexTable[b >> 4];
            destination[i * 2 + 1] = (char)hexTable[b & 0xF];
        }
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? HexLength + 2 : HexLength;

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

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? HexLength + 2 : HexLength;

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

    private void FormatHexCoreUtf8(Span<byte> destination, bool uppercase)
    {
        if (_bytes == null) return;

        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        for (int i = 0; i < _bytes.Length; i++)
        {
            byte b = _bytes[i];
            destination[i * 2] = hexTable[b >> 4];
            destination[i * 2 + 1] = hexTable[b & 0xF];
        }
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Implicit conversion from byte array.
    /// </summary>
    public static implicit operator HexBytes(byte[] bytes) => new(bytes);

    /// <summary>
    /// Implicit conversion from span.
    /// </summary>
    public static implicit operator HexBytes(ReadOnlySpan<byte> bytes) => new(bytes);

    /// <summary>
    /// Implicit conversion to ReadOnlySpan.
    /// </summary>
    public static implicit operator ReadOnlySpan<byte>(HexBytes value) => value.Span;

    /// <summary>
    /// Implicit conversion to ReadOnlyMemory.
    /// </summary>
    public static implicit operator ReadOnlyMemory<byte>(HexBytes value) => value.Memory;

    #endregion

    #region EVM Helpers

    /// <summary>
    /// Extracts the function selector (first 4 bytes) from calldata.
    /// </summary>
    public FunctionSelector GetFunctionSelector()
    {
        if (Length < 4)
            throw new InvalidOperationException("Calldata must be at least 4 bytes for function selector");
        return new FunctionSelector(Span.Slice(0, 4));
    }

    /// <summary>
    /// Gets the calldata without the function selector.
    /// </summary>
    public HexBytes GetCalldataParams()
    {
        if (Length <= 4)
            return Empty;
        return Slice(4);
    }

    /// <summary>
    /// Pads the bytes to a multiple of 32 bytes (EVM word size).
    /// </summary>
    public HexBytes PadToWord()
    {
        if (Length == 0) return Empty;
        int padding = (32 - (Length % 32)) % 32;
        if (padding == 0) return this;
        
        var result = new byte[Length + padding];
        Span.CopyTo(result);
        return FromArrayUnsafe(result);
    }

    /// <summary>
    /// Pads the bytes to a specific length (right-padded with zeros).
    /// </summary>
    public HexBytes PadRight(int length)
    {
        if (Length >= length) return this;
        var result = new byte[length];
        Span.CopyTo(result);
        return FromArrayUnsafe(result);
    }

    /// <summary>
    /// Pads the bytes to a specific length (left-padded with zeros).
    /// </summary>
    public HexBytes PadLeft(int length)
    {
        if (Length >= length) return this;
        var result = new byte[length];
        Span.CopyTo(result.AsSpan(length - Length));
        return FromArrayUnsafe(result);
    }

    #endregion
}

