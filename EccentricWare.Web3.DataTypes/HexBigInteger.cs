using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// Represents an arbitrarily large signed integer used for blockchain interop (EVM/Solana),
/// supporting JSON-RPC hex quantities (e.g., "0x1a") and negative values (e.g., "-0x1").
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
    /// <summary>
    /// Maximum allowed hex digit count when parsing untrusted JSON-RPC tokens.
    /// This limit is primarily for DoS hygiene; adjust per deployment constraints.
    /// </summary>
    public const int DefaultMaxHexDigits = 4096;

    private readonly BigInteger _value;

    /// <summary>Represents the value 0.</summary>
    public static readonly HexBigInteger Zero = BigInteger.Zero;

    /// <summary>Represents the value 1.</summary>
    public static readonly HexBigInteger One = BigInteger.One;

    /// <summary>Represents the value -1.</summary>
    public static readonly HexBigInteger MinusOne = BigInteger.MinusOne;

    #region Constructors

    /// <summary>
    /// Creates an instance from a <see cref="BigInteger"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(BigInteger value) => _value = value;

    /// <summary>
    /// Creates an instance from a signed 64-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(long value) => _value = new BigInteger(value);

    /// <summary>
    /// Creates an instance from an unsigned 64-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(ulong value) => _value = new BigInteger(value);

    /// <summary>
    /// Creates an instance from a big-endian byte array representing the magnitude.
    /// </summary>
    /// <param name="bigEndianBytes">Magnitude bytes in big-endian order.</param>
    /// <param name="isUnsigned">True to interpret the input as unsigned magnitude.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexBigInteger(ReadOnlySpan<byte> bigEndianBytes, bool isUnsigned)
        => _value = new BigInteger(bigEndianBytes, isUnsigned: isUnsigned, isBigEndian: true);

    #endregion

    #region Core properties

    /// <summary>
    /// Gets the underlying <see cref="BigInteger"/> value.
    /// </summary>
    public BigInteger Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Gets the sign of the value: -1, 0, or 1.
    /// </summary>
    public int Sign
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign;
    }

    /// <summary>
    /// Returns true if the value is zero.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsZero;
    }

    /// <summary>
    /// Returns true if the value is negative.
    /// </summary>
    public bool IsNegative
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Sign < 0;
    }

    /// <summary>
    /// Returns the number of bytes required to represent the magnitude.
    /// </summary>
    public int MagnitudeByteCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.GetByteCount(isUnsigned: true);
    }

    #endregion

    #region Equality and comparison

    /// <summary>
    /// Determines whether this instance equals another.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(HexBigInteger other) => _value.Equals(other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is HexBigInteger other && Equals(other);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(HexBigInteger other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is HexBigInteger other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(HexBigInteger)}", nameof(obj));
    }

    /// <summary>Equality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(HexBigInteger left, HexBigInteger right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HexBigInteger left, HexBigInteger right) => !left.Equals(right);

    /// <summary>Less-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(HexBigInteger left, HexBigInteger right) => left._value < right._value;

    /// <summary>Greater-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(HexBigInteger left, HexBigInteger right) => left._value > right._value;

    /// <summary>Less-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(HexBigInteger left, HexBigInteger right) => left._value <= right._value;

    /// <summary>Greater-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(HexBigInteger left, HexBigInteger right) => left._value >= right._value;

    #endregion

    #region Conversions

    /// <summary>Implicit conversion from <see cref="BigInteger"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(BigInteger value) => new(value);

    /// <summary>Implicit conversion to <see cref="BigInteger"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(HexBigInteger value) => value._value;

    /// <summary>
    /// Converts to uint256 (throws if negative or exceeds 256 bits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256 ToUInt256() => (uint256)_value;

    #endregion

    #region Parsing (hot path: UTF-8 token normalisation + bounded decode)

    /// <summary>
    /// Parses a JSON-RPC-like UTF-8 token containing a hex quantity (quoted or unquoted), with DoS bounds.
    /// Accepts: <c>null</c>, <c>"0x..."</c>, <c>0x...</c>, <c>"-0x..."</c>.
    /// </summary>
    /// <param name="utf8Token">UTF-8 token.</param>
    /// <param name="result">Parsed value (zero for <c>null</c>).</param>
    /// <param name="maxHexDigits">Maximum allowed hex digit count.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParseUtf8(ReadOnlySpan<byte> utf8Token, out HexBigInteger result, int maxHexDigits = DefaultMaxHexDigits)
    {
        result = default;

        if (!ByteUtils.TryNormaliseHexTokenUtf8(utf8Token, out bool isNull, out bool isNegative, out var hexDigitsUtf8))
            return false;

        if (isNull)
        {
            result = default; // zero; presence must be tracked externally if needed.
            return true;
        }

        if (!ByteUtils.TryParseBigIntegerHexUnsignedUtf8(hexDigitsUtf8, maxHexDigits, out BigInteger magnitude))
            return false;

        if (isNegative && magnitude != BigInteger.Zero)
            magnitude = BigInteger.Negate(magnitude);

        result = new HexBigInteger(magnitude);
        return true;
    }

    /// <summary>
    /// Parses a UTF-8 token or throws if invalid.
    /// </summary>
    /// <param name="utf8Token">UTF-8 token.</param>
    /// <param name="maxHexDigits">Maximum allowed hex digit count.</param>
    public static HexBigInteger ParseUtf8(ReadOnlySpan<byte> utf8Token, int maxHexDigits = DefaultMaxHexDigits)
    {
        if (TryParseUtf8(utf8Token, out var value, maxHexDigits))
            return value;

        throw new FormatException("Invalid hex quantity token.");
    }

    #endregion

    #region Parsing (ISpanParsable / chars)

    /// <inheritdoc />
    public static HexBigInteger Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var value))
            return value;

        throw new FormatException("Invalid hexadecimal string.");
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HexBigInteger result)
    {
        result = default;

        s = ByteUtils.TrimAsciiWhitespace(s);

        if (s.IsEmpty)
            return false;

        if (!ByteUtils.TryTrimLeadingSign(s, out bool isNegative, out var unsignedSpan))
            return false;

        unsignedSpan = ByteUtils.TrimAsciiWhitespace(unsignedSpan);

        if (ByteUtils.TryTrimHexPrefix(unsignedSpan, out var afterPrefix))
            unsignedSpan = afterPrefix;

        if (unsignedSpan.IsEmpty)
        {
            result = default;
            return true;
        }

        if (!ByteUtils.IsAllHexChars(unsignedSpan))
            return false;

        if (!BigInteger.TryParse(unsignedSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var magnitude))
            return false;

        if (isNegative && magnitude != BigInteger.Zero)
            magnitude = BigInteger.Negate(magnitude);

        result = new HexBigInteger(magnitude);
        return true;
    }

    /// <inheritdoc />
    public static HexBigInteger Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out HexBigInteger result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), provider, out result);
    }

    #endregion

    #region Formatting (hot path: bytes -> hex; cold path: ToString)

    /// <inheritdoc />
    public override string ToString() => ToString("0x", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "0x";

        if (format is "d" or "D")
            return _value.ToString(CultureInfo.InvariantCulture);

        bool uppercase = format is "X" or "0X";
        bool withPrefix = format is "0x" or "0X" || format.Length == 0;
        bool rawHex = format is "x" or "X";

        if (!(withPrefix || rawHex))
            throw new FormatException($"Unknown format: {format}");

        // Cold path: string allocation is acceptable here.
        if (_value.IsZero)
            return withPrefix ? "0x0" : "0";

        if (_value.Sign < 0)
        {
            string absHex = BigInteger.Abs(_value).ToString(uppercase ? "X" : "x", CultureInfo.InvariantCulture);
            return withPrefix ? "-0x" + absHex : "-" + absHex;
        }

        string hex = _value.ToString(uppercase ? "X" : "x", CultureInfo.InvariantCulture);
        return withPrefix ? "0x" + hex : hex;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        // For simplicity and correctness, route char formatting through UTF-8 formatting (ASCII subset).
        Span<byte> tmpUtf8 = stackalloc byte[Math.Min(512, destination.Length)];
        if (!TryFormat(tmpUtf8, out int bytesWritten, format, provider))
        {
            charsWritten = 0;
            return false;
        }

        if (bytesWritten > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        for (int i = 0; i < bytesWritten; i++)
            destination[i] = (char)tmpUtf8[i];

        charsWritten = bytesWritten;
        return true;
    }


    public bool TryFormat(
     Span<byte> utf8Destination,
     out int bytesWritten,
     ReadOnlySpan<char> format = default,
     IFormatProvider? provider = null)
    {
        bytesWritten = 0;

        // Decimal path: BigInteger only supports Span<char> formatting.
        bool wantsDecimal = format.Length == 1 && (format[0] == 'd' || format[0] == 'D');
        if (wantsDecimal)
        {
            int charCapacity = utf8Destination.Length;
            if (charCapacity == 0)
                return false;

            const int StackCharLimit = 256;
            char[]? rentedChars = null;

            try
            {
                Span<char> charBuffer = charCapacity <= StackCharLimit
                    ? stackalloc char[charCapacity]
                    : (rentedChars = ArrayPool<char>.Shared.Rent(charCapacity));

                if (rentedChars is not null)
                    charBuffer = charBuffer.Slice(0, charCapacity);

                if (!_value.TryFormat(charBuffer, out int charsWritten, format, provider))
                    return false;

                if ((uint)charsWritten > (uint)utf8Destination.Length)
                    return false;

                // Decimal output is ASCII; direct cast is safe.
                for (int i = 0; i < charsWritten; i++)
                    utf8Destination[i] = (byte)charBuffer[i];

                bytesWritten = charsWritten;
                return true;
            }
            finally
            {
                if (rentedChars is not null)
                    ArrayPool<char>.Shared.Return(rentedChars);
            }
        }

        // Hex path.
        bool withPrefix;
        bool uppercase;

        if (format.Length == 0)
        {
            // Default: "0x" prefixed, lowercase.
            withPrefix = true;
            uppercase = false;
        }
        else if (format.Length == 1)
        {
            char f = format[0];
            if (f == 'x')
            {
                withPrefix = false;
                uppercase = false;
            }
            else if (f == 'X')
            {
                withPrefix = false;
                uppercase = true;
            }
            else
            {
                return false;
            }
        }
        else if (format.Length == 2)
        {
            // "0x" / "0X"
            if (format[0] != '0' || (format[1] != 'x' && format[1] != 'X'))
                return false;

            withPrefix = true;
            uppercase = (format[1] == 'X');
        }
        else
        {
            return false;
        }

        // Zero: fast constant output.
        if (_value.IsZero)
        {
            if (withPrefix)
            {
                if (utf8Destination.Length < 3) return false;
                utf8Destination[0] = (byte)'0';
                utf8Destination[1] = (byte)'x';
                utf8Destination[2] = (byte)'0';
                bytesWritten = 3;
                return true;
            }

            if (utf8Destination.Length < 1) return false;
            utf8Destination[0] = (byte)'0';
            bytesWritten = 1;
            return true;
        }

        int pos = 0;

        // Leading '-' for negative values.
        bool isNegative = _value.Sign < 0;
        BigInteger absValue = isNegative ? BigInteger.Abs(_value) : _value;

        if (isNegative)
        {
            if (utf8Destination.Length < 1)
                return false;

            utf8Destination[pos++] = (byte)'-';
        }

        int magnitudeByteCount = absValue.GetByteCount(isUnsigned: true);
        if (magnitudeByteCount == 0)
            magnitudeByteCount = 1;

        const int StackByteLimit = 128;
        byte[]? rentedBytes = null;

        try
        {
            Span<byte> magnitudeBigEndian = magnitudeByteCount <= StackByteLimit
                ? stackalloc byte[magnitudeByteCount]
                : (rentedBytes = ArrayPool<byte>.Shared.Rent(magnitudeByteCount));

            if (rentedBytes is not null)
                magnitudeBigEndian = magnitudeBigEndian.Slice(0, magnitudeByteCount);

            if (!absValue.TryWriteBytes(magnitudeBigEndian, out int written, isUnsigned: true, isBigEndian: true))
                return false;

            if (!ByteUtils.TryWriteHexMinimalUtf8(
                unsignedMagnitudeBigEndian: magnitudeBigEndian.Slice(0, written),
                write0xPrefix: withPrefix,
                uppercase: uppercase,
                destinationUtf8: utf8Destination.Slice(pos),
                bytesWritten: out int hexBytesWritten))
            {
                return false;
            }

            bytesWritten = pos + hexBytesWritten;
            return true;
        }
        finally
        {
            if (rentedBytes is not null)
                ArrayPool<byte>.Shared.Return(rentedBytes);
        }
    }

    #endregion
}