using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A BigInteger wrapper optimized for EVM and Solana blockchain operations.
/// Provides hex string parsing and display with minimal overhead.
/// Supports arbitrary precision for values larger than 256 bits or negative values.
/// </summary>
[JsonConverter(typeof(HexBigIntegerJsonConverter))]
public readonly struct HexBigInteger : 
    IEquatable<HexBigInteger>, 
    IComparable<HexBigInteger>, 
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<HexBigInteger>
{
    private readonly BigInteger _value;

    public static readonly HexBigInteger Zero;
    public static readonly HexBigInteger One = new(BigInteger.One);
    public static readonly HexBigInteger MinusOne = new(BigInteger.MinusOne);

    #region Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(BigInteger value) => _value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(long value) => _value = new BigInteger(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(ulong value) => _value = new BigInteger(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(int value) => _value = new BigInteger(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(uint value) => _value = new BigInteger(value);

    /// <summary>
    /// Creates a HexBigInteger from a big-endian byte array.
    /// Compatible with EVM and Solana encoding (unsigned by default).
    /// </summary>
    public HexBigInteger(ReadOnlySpan<byte> bigEndianBytes, bool isUnsigned = true)
    {
        _value = new BigInteger(bigEndianBytes, isUnsigned, isBigEndian: true);
    }

    public HexBigInteger(ReadOnlySpan<byte> utf8Bytes)
    {
        _value = Parse(utf8Bytes);
    }

    /// <summary>
    /// Creates a HexBigInteger from a little-endian byte array.
    /// Compatible with Solana native encoding.
    /// </summary>
    public static HexBigInteger FromLittleEndian(ReadOnlySpan<byte> littleEndianBytes, bool isUnsigned = true)
    {
        return new HexBigInteger(new BigInteger(littleEndianBytes, isUnsigned, isBigEndian: false));
    }

    #endregion

    /// <summary>
    /// Gets the underlying BigInteger value.
    /// </summary>
    public BigInteger Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(HexBigInteger other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is HexBigInteger other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => _value.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(HexBigInteger left, HexBigInteger right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HexBigInteger left, HexBigInteger right) => !left.Equals(right);

    #endregion

    #region Comparison

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(HexBigInteger other) => _value.CompareTo(other._value);

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is HexBigInteger other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(HexBigInteger)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(HexBigInteger left, HexBigInteger right) => left._value < right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(HexBigInteger left, HexBigInteger right) => left._value > right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(HexBigInteger left, HexBigInteger right) => left._value <= right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(HexBigInteger left, HexBigInteger right) => left._value >= right._value;

    #endregion

    #region Arithmetic Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator +(HexBigInteger left, HexBigInteger right)
        => new(left._value + right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator -(HexBigInteger left, HexBigInteger right)
        => new(left._value - right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator *(HexBigInteger left, HexBigInteger right)
        => new(left._value * right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator /(HexBigInteger left, HexBigInteger right)
        => new(left._value / right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator %(HexBigInteger left, HexBigInteger right)
        => new(left._value % right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator -(HexBigInteger value)
        => new(-value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator ++(HexBigInteger value)
        => new(value._value + BigInteger.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator --(HexBigInteger value)
        => new(value._value - BigInteger.One);

    /// <summary>
    /// Returns the absolute value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger Abs() => new(BigInteger.Abs(_value));

    /// <summary>
    /// Returns the power of this value raised to the specified exponent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger Pow(int exponent) => new(BigInteger.Pow(_value, exponent));

    /// <summary>
    /// Performs modular exponentiation: (this ^ exponent) mod modulus.
    /// Used in cryptographic operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger ModPow(HexBigInteger exponent, HexBigInteger modulus)
        => new(BigInteger.ModPow(_value, exponent._value, modulus._value));

    #endregion

    #region Bitwise Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator &(HexBigInteger left, HexBigInteger right)
        => new(left._value & right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator |(HexBigInteger left, HexBigInteger right)
        => new(left._value | right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator ^(HexBigInteger left, HexBigInteger right)
        => new(left._value ^ right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator ~(HexBigInteger value)
        => new(~value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator <<(HexBigInteger value, int shift)
        => new(value._value << shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger operator >>(HexBigInteger value, int shift)
        => new(value._value >> shift);

    #endregion

    #region Conversions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(BigInteger value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(long value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(ulong value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(int value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(uint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(HexBigInteger value) => value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator long(HexBigInteger value) => (long)value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ulong(HexBigInteger value) => (ulong)value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(HexBigInteger value) => (int)value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(HexBigInteger value) => (uint)value._value;

    /// <summary>
    /// Converts to uint256 (throws if value is negative or exceeds 256 bits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256 ToUInt256() => (uint256)_value;



    /// <summary>
    /// Creates from uint256.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger FromUInt256(uint256 value) => value;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hexadecimal string (with or without 0x prefix).
    /// Supports negative values with leading '-'.
    /// </summary>
    /// 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger Parse(ReadOnlySpan<byte> hex)
    {
        if(TryParse(hex, out var result))
        {
            return result; 
        }
         
        throw new FormatException("Invalid hexadecimal string");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger Parse(ReadOnlySpan<char> hex)
    {
        bool negative = false;
        if (hex.Length > 0 && hex[0] == '-')
        {
            negative = true;
            hex = hex.Slice(1);
        }

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return Zero;

        if (!BigInteger.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new FormatException("Invalid hexadecimal string");

        return new HexBigInteger(negative ? -value : value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
        {
            return result;
        }

        throw new FormatException("Invalid hexadecimal string");
    }
    public static HexBigInteger Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hexadecimal string without throwing exceptions.
    /// </summary>
    /// 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> hex, out HexBigInteger result)
    {
        result = default!;

        if (hex.Length == 0)
            return false;

        // null
        if (hex.SequenceEqual("null"u8))
            return true;

        // String token: "0x..."
        if (hex[0] == (byte)'"')
        {
            if (hex.Length < 4 || hex[^1] != (byte)'"')
                return false;

            var inner = hex.Slice(1, hex.Length - 2);

            if (inner.Length >= 2 &&
                inner[0] == (byte)'0' &&
                (inner[1] == (byte)'x' || inner[1] == (byte)'X'))
            {
                var hexInner = inner.Slice(2);
                if (hexInner.Length == 0)
                {
                    result = new HexBigInteger(BigInteger.Zero);
                    return true;
                }

                if (!TryParseHexBigEndian(hexInner, out var bi))
                    return false;

                result = new HexBigInteger(bi);
                return true;
            }

            return false;
        }

        // Numeric token (rare but legal in some clients)
        // Parse as decimal, no allocations
        //if (TryParseDecimal(hex, out var dec))
        //{
        //    result = new HexBigInteger(dec);
        //    return true;
        //}

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> hex, out HexBigInteger result)
    {
        result = Zero;

        if (hex.Length == 0)
            return false;

        bool negative = false;
        if (hex[0] == '-')
        {
            negative = true;
            hex = hex.Slice(1);
        }

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
        {
            result = Zero;
            return true; // "0x" or "-0x" is zero
        }

        // Validate hex characters before parsing
        foreach (char c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        if (!BigInteger.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        result = new HexBigInteger(negative ? -value : value);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? hex, out HexBigInteger result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HexBigInteger result)
        => TryParse(s, out result);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out HexBigInteger result)
        => TryParse(s, out result);

    /// <summary>
    /// Parses a decimal string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger ParseDecimal(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
            return Zero;

        if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var bigInt))
            throw new FormatException("Invalid decimal string");

        return new HexBigInteger(bigInt);
    }

    /// <summary>
    /// Tries to parse a decimal string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDecimal(ReadOnlySpan<char> value, out HexBigInteger result)
    {
        result = Zero;

        if (value.Length == 0)
            return true;

        if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var bigInt))
            return false;

        result = new HexBigInteger(bigInt);
        return true;
    }

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the hexadecimal representation with 0x prefix.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the value according to the format string.
    /// "x" or "X" for hex without prefix, "0x" for hex with prefix (default), "d" for decimal.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        if (format == "0x" || format == "0X")
        {
            if (_value.IsZero)
                return "0x0";

            if (_value.Sign < 0)
                return "-0x" + (-_value).ToString("x", CultureInfo.InvariantCulture);

            return "0x" + _value.ToString("x", CultureInfo.InvariantCulture);
        }

        if (format == "x")
        {
            if (_value.IsZero)
                return "0";

            if (_value.Sign < 0)
                return "-" + (-_value).ToString("x", CultureInfo.InvariantCulture);

            return _value.ToString("x", CultureInfo.InvariantCulture);
        }

        if (format == "X")
        {
            if (_value.IsZero)
                return "0";

            if (_value.Sign < 0)
                return "-" + (-_value).ToString("X", CultureInfo.InvariantCulture);

            return _value.ToString("X", CultureInfo.InvariantCulture);
        }

        if (format == "D" || format == "d")
        {
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        throw new FormatException($"Unknown format: {format}");
    }

    /// <summary>
    /// Tries to format the value into the destination span.
    /// Zero-allocation implementation that writes directly to the destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        charsWritten = 0;
        bool uppercase = format.Length == 1 && format[0] == 'X';
        bool withPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool isDecimal = format.Length == 1 && (format[0] == 'D' || format[0] == 'd');

        if (isDecimal)
        {
            return _value.TryFormat(destination, out charsWritten, default, provider);
        }

        int pos = 0;
        bool negative = _value.Sign < 0;
        var absValue = negative ? -_value : _value;

        // Write negative sign if needed
        if (negative)
        {
            if (destination.Length < 1)
                return false;
            destination[pos++] = '-';
        }

        // Write prefix if needed
        if (withPrefix)
        {
            if (destination.Length - pos < 2)
                return false;
            destination[pos++] = '0';
            destination[pos++] = 'x';
        }

        // Handle zero case
        if (_value.IsZero)
        {
            if (destination.Length - pos < 1)
                return false;
            destination[pos++] = '0';
            charsWritten = pos;
            return true;
        }

        // Format the absolute value as hex using BigInteger's TryFormat
        ReadOnlySpan<char> hexFormat = uppercase ? "X" : "x";
        if (!absValue.TryFormat(destination.Slice(pos), out int written, hexFormat, provider))
            return false;
        charsWritten = pos + written;
        return true;
    }

    /// <summary>
    /// Tries to format the value into the destination UTF-8 byte span.
    /// Zero heap allocation for typical blockchain values (up to 1024 bits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        // Estimate required size: 2 hex chars per byte + prefix + sign
        int byteCount = _value.IsZero ? 1 : _value.GetByteCount(isUnsigned: _value.Sign >= 0);
        int estimatedChars = (byteCount * 2) + 4; // hex digits + "-0x" + buffer

        // Use stack allocation for typical sizes (up to ~1024 bits)
        if (estimatedChars <= 260)
        {
            Span<char> buffer = stackalloc char[260];
            if (!TryFormat(buffer, out int charsWritten, format, provider))
            {
                bytesWritten = 0;
                return false;
            }

            if (charsWritten > utf8Destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            // All output characters are ASCII, so direct byte conversion is safe
            for (int i = 0; i < charsWritten; i++)
            {
                utf8Destination[i] = (byte)buffer[i];
            }
            bytesWritten = charsWritten;
            return true;
        }

        // Fallback for very large values - use heap allocation
        var str = ToString(format.Length == 0 ? null : new string(format), provider);
        if (str.Length > utf8Destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < str.Length; i++)
        {
            utf8Destination[i] = (byte)str[i];
        }
        bytesWritten = str.Length;
        return true;
    }

    /// <summary>
    /// Returns the hexadecimal representation with specified minimum digits (no 0x prefix).
    /// Pads with leading zeros if necessary. Optimized for minimal allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToHexString(int minDigits = 0)
    {
        if (_value.IsZero)
            return minDigits <= 1 ? "0" : new string('0', minDigits);

        var abs = _value.Sign < 0 ? -_value : _value;
        string hex = abs.ToString("x", CultureInfo.InvariantCulture);
        
        int padding = Math.Max(0, minDigits - hex.Length);
        bool negative = _value.Sign < 0;

        if (padding == 0 && !negative)
            return hex;

        if (padding == 0)
            return "-" + hex;

        // Use string.Create for single allocation
        return string.Create(
            (negative ? 1 : 0) + padding + hex.Length,
            (negative, padding, hex),
            static (span, state) =>
            {
                int pos = 0;
                if (state.negative)
                    span[pos++] = '-';
                span.Slice(pos, state.padding).Fill('0');
                state.hex.AsSpan().CopyTo(span.Slice(pos + state.padding));
            });
    }

    /// <summary>
    /// Returns the decimal string representation.
    /// </summary>
    public string ToDecimalString() => _value.ToString(CultureInfo.InvariantCulture);

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Returns the value as a big-endian byte array.
    /// Compatible with EVM and Solana encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBigEndianBytes()
    {
        return _value.ToByteArray(isUnsigned: _value.Sign >= 0, isBigEndian: true);
    }

    /// <summary>
    /// Returns the value as a little-endian byte array.
    /// Compatible with Solana native encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToLittleEndianBytes()
    {
        return _value.ToByteArray(isUnsigned: _value.Sign >= 0, isBigEndian: false);
    }

    /// <summary>
    /// Returns the value as a big-endian byte array, padded to the specified length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBigEndianBytes(int length)
    {
        byte[] bytes = ToBigEndianBytes();
        if (bytes.Length >= length)
            return bytes;

        byte[] padded = new byte[length];
        bytes.CopyTo(padded, length - bytes.Length);
        return padded;
    }

    /// <summary>
    /// Writes the value as a big-endian byte array to the destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteBigEndian(Span<byte> destination)
    {
        return _value.TryWriteBytes(destination, out int bytesWritten, isUnsigned: _value.Sign >= 0, isBigEndian: true)
            ? bytesWritten
            : throw new ArgumentException("Destination too small", nameof(destination));
    }

    /// <summary>
    /// Writes the value as a little-endian byte array to the destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteLittleEndian(Span<byte> destination)
    {
        return _value.TryWriteBytes(destination, out int bytesWritten, isUnsigned: _value.Sign >= 0, isBigEndian: false)
            ? bytesWritten
            : throw new ArgumentException("Destination too small", nameof(destination));
    }

    /// <summary>
    /// Tries to write the value as big-endian bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBigEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteBytes(destination, out bytesWritten, isUnsigned: _value.Sign >= 0, isBigEndian: true);

    /// <summary>
    /// Tries to write the value as little-endian bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteLittleEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteBytes(destination, out bytesWritten, isUnsigned: _value.Sign >= 0, isBigEndian: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexBigEndian(
        ReadOnlySpan<byte> hex,
        out BigInteger value)
    {
        value = BigInteger.Zero;

        int byteLen = (hex.Length + 1) >> 1;
        Span<byte> tmp = byteLen <= 64
            ? stackalloc byte[byteLen]
            : new byte[byteLen];

        int hi = hex.Length & 1;
        int src = 0;
        int dst = 0;

        if (hi == 1)
        {
            if (!ByteUtils.TryHexNibble(hex[src++], out var n))
                return false;

            tmp[dst++] = n;
        }

        while (src < hex.Length)
        {
            if (!ByteUtils.TryHexNibble(hex[src++], out var hiNib) ||
                !ByteUtils.TryHexNibble(hex[src++], out var loNib))
                return false;

            tmp[dst++] = (byte)((hiNib << 4) | loNib);
        }

        // BigInteger expects little-endian; reverse
        tmp[..dst].Reverse();
        value = new BigInteger(tmp[..dst], isUnsigned: true, isBigEndian: false);
        return true;
    }

    #endregion

    #region Properties

    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsZero;
    }

    public bool IsOne
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsOne;
    }

    public bool IsNegative
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign < 0;
    }

    public bool IsPositive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign > 0;
    }

    public int Sign
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign;
    }

    /// <summary>
    /// Returns the number of bytes needed to represent the value.
    /// </summary>
    public int ByteCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.GetByteCount(isUnsigned: _value.Sign >= 0);
    }

    /// <summary>
    /// Returns true if the value fits in a uint256 (non-negative and ≤ 256 bits).
    /// </summary>
    public bool FitsInUInt256
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign >= 0 && _value.GetByteCount(isUnsigned: true) <= 32;
    }

    /// <summary>
    /// Returns true if the value fits in an int256 (within signed 256-bit range).
    /// </summary>
    public bool FitsInInt256
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.GetByteCount(isUnsigned: false) <= 32;
    }

    #endregion

    #region EVM/Solana Helpers

    /// <summary>
    /// Returns the value as a 32-byte ABI-encoded value (big-endian, left-padded).
    /// Standard EVM ABI encoding for uint256/int256.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToAbiEncoded()
    {
        if (_value.Sign < 0)
        {
            // Two's complement for negative values
            BigInteger twosComplement = (BigInteger.One << 256) + _value;
            Span<byte> bytes = stackalloc byte[32];
            twosComplement.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: true);
            return bytes.ToArray();
        }

        return ToBigEndianBytes(32);
    }

    /// <summary>
    /// Parses an ABI-encoded value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger FromAbiEncoded(ReadOnlySpan<byte> data, bool isSigned = false)
    {
        if (data.Length < 32)
            throw new ArgumentException("ABI encoded value requires at least 32 bytes", nameof(data));

        BigInteger value = new BigInteger(data.Slice(0, 32), isUnsigned: !isSigned, isBigEndian: true);
        
        if (isSigned && value > (BigInteger.One << 255) - 1)
        {
            // Convert from two's complement
            value -= BigInteger.One << 256;
        }

        return new HexBigInteger(value);
    }

    /// <summary>
    /// Converts to EVM-style minimal hex string (no leading zeros, lowercase).
    /// </summary>
    public string ToEvmHex() => ToString("0x", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts a wei value to ether.
    /// </summary>
    public decimal ToEther()
    {
        return (decimal)_value / 1_000_000_000_000_000_000m;
    }

    /// <summary>
    /// Converts a lamports value to SOL.
    /// </summary>
    public decimal ToSol()
    {
        return (decimal)_value / 1_000_000_000m;
    }

    /// <summary>
    /// Creates a wei value from ether.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger FromEther(decimal ether)
    {
        var wei = ether * 1_000_000_000_000_000_000m;
        return new HexBigInteger(new BigInteger(wei));
    }

    /// <summary>
    /// Creates a lamports value from SOL.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexBigInteger FromSol(decimal sol)
    {
        var lamports = sol * 1_000_000_000m;
        return new HexBigInteger(new BigInteger(lamports));
    }

    #endregion
}
