using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// Represents a 256-bit signed integer using two's complement semantics with a fixed 32-byte layout (4 x 64-bit limbs).
/// Optimised for Web3 workloads (EVM/Solana), including allocation-free parsing of hex/decimal and efficient comparisons.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct int256 :
    IEquatable<int256>,
    IComparable<int256>,
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<int256>
{
    /// <summary>Least significant 64 bits (bits 0..63).</summary>
    private readonly ulong _u0;
    /// <summary>Bits 64..127.</summary>
    private readonly ulong _u1;
    /// <summary>Bits 128..191.</summary>
    private readonly ulong _u2;
    /// <summary>Most significant 64 bits (bits 192..255). The sign bit is bit 255 (MSB of this limb).</summary>
    private readonly ulong _u3;

    /// <summary>Represents the value 0.</summary>
    public static readonly int256 Zero = new int256(0);

    /// <summary>Represents the value 1.</summary>
    public static readonly int256 One = new int256(1L);

    /// <summary>Represents the value -1.</summary>
    public static readonly int256 MinusOne = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    /// <summary>Represents the minimum possible value (-2^255).</summary>
    public static readonly int256 MinValue = new(0, 0, 0, 0x8000_0000_0000_0000UL);

    /// <summary>Represents the maximum possible value (2^255 - 1).</summary>
    public static readonly int256 MaxValue = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0x7FFF_FFFF_FFFF_FFFFUL);

    #region Constructors

    /// <summary>
    /// Creates a signed 256-bit value from a 64-bit signed integer.
    /// </summary>
    /// <param name="value">The signed 64-bit value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256(long value)
    {
        _u0 = unchecked((ulong)value);
        _u1 = value < 0 ? ulong.MaxValue : 0UL;
        _u2 = value < 0 ? ulong.MaxValue : 0UL;
        _u3 = value < 0 ? ulong.MaxValue : 0UL;
    }

    /// <summary>
    /// Creates a signed 256-bit value from four 64-bit limbs (little-endian limb order).
    /// The limbs are interpreted as a two's complement 256-bit value.
    /// </summary>
    /// <param name="u0">Least significant 64 bits.</param>
    /// <param name="u1">Bits 64..127.</param>
    /// <param name="u2">Bits 128..191.</param>
    /// <param name="u3">Most significant 64 bits (contains the sign bit).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    /// <summary>
    /// Creates a signed 256-bit value from a big-endian 0..32 byte sequence interpreted as a two's complement integer.
    /// If fewer than 32 bytes are provided, the value is sign-extended from the most significant provided byte.
    /// </summary>
    /// <param name="bigEndianTwosComplementBytes">Two's complement bytes in big-endian order.</param>
    public int256(ReadOnlySpan<byte> bigEndianTwosComplementBytes)
    {
        if (bigEndianTwosComplementBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes.", nameof(bigEndianTwosComplementBytes));

        Span<byte> padded = stackalloc byte[32];

        if (bigEndianTwosComplementBytes.Length == 0)
        {
            padded.Clear();
        }
        else
        {
            // Sign extension based on MSB of first provided byte.
            byte signFill = (bigEndianTwosComplementBytes[0] & 0x80) != 0 ? (byte)0xFF : (byte)0x00;
            padded.Fill(signFill);
            bigEndianTwosComplementBytes.CopyTo(padded.Slice(32 - bigEndianTwosComplementBytes.Length));
        }

        _u3 = BinaryPrimitives.ReadUInt64BigEndian(padded);
        _u2 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(8));
        _u1 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(16));
        _u0 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(24));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether this value is zero.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    /// <summary>
    /// Gets a value indicating whether this value is negative.
    /// </summary>
    public bool IsNegative
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u3 & 0x8000_0000_0000_0000UL) != 0;
    }

    /// <summary>
    /// Gets the sign of this value: -1 for negative, 0 for zero, 1 for positive.
    /// </summary>
    public int Sign
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsZero ? 0 : (IsNegative ? -1 : 1);
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the raw two's complement value as a 32-byte big-endian representation into the destination span.
    /// </summary>
    /// <param name="destination">The destination span that must be at least 32 bytes long.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u3);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u0);
    }

    /// <summary>
    /// Writes the raw two's complement value as a 32-byte little-endian representation into the destination span.
    /// </summary>
    /// <param name="destination">The destination span that must be at least 32 bytes long.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Returns the raw two's complement value as a new 32-byte big-endian array.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        byte[] bytes = new byte[32];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the raw two's complement value as a new 32-byte little-endian array.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        byte[] bytes = new byte[32];
        WriteLittleEndian(bytes);
        return bytes;
    }

    #endregion

    #region Equality and Hashing

    /// <summary>
    /// Determines whether this value equals another value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(int256 other)
    {
        ulong diff = (_u0 ^ other._u0) | (_u1 ^ other._u1) | (_u2 ^ other._u2) | (_u3 ^ other._u3);
        return diff == 0;
    }

    /// <summary>
    /// Determines whether this value equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is int256 other && Equals(other);

    /// <summary>
    /// Computes a high-quality hash code suitable for large hash tables (millions of keys).
    /// </summary>
    public override int GetHashCode()
    {
        static ulong Mix(ulong x)
        {
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return x;
        }

        ulong h = 0x9e3779b97f4a7c15UL;
        h = Mix(h ^ _u0);
        h = Mix(h ^ _u1);
        h = Mix(h ^ _u2);
        h = Mix(h ^ _u3);

        return (int)(h ^ (h >> 32));
    }

    /// <summary>
    /// Determines whether two values are equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(int256 left, int256 right) => left.Equals(right);

    /// <summary>
    /// Determines whether two values are not equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(int256 left, int256 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Compares this value to another signed 256-bit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(int256 other)
    {
        bool negA = IsNegative;
        bool negB = other.IsNegative;

        if (negA != negB)
            return negA ? -1 : 1;

        // For two's complement, unsigned limb ordering is monotonic within the negative range and within the non-negative range.
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        return 0;
    }

    /// <summary>
    /// Compares this value to another object.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is int256 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(int256)}.", nameof(obj));
    }

    /// <summary>Returns true if left is less than right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(int256 left, int256 right) => left.CompareTo(right) < 0;

    /// <summary>Returns true if left is greater than right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(int256 left, int256 right) => left.CompareTo(right) > 0;

    /// <summary>Returns true if left is less than or equal to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(int256 left, int256 right) => left.CompareTo(right) <= 0;

    /// <summary>Returns true if left is greater than or equal to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(int256 left, int256 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Arithmetic

    /// <summary>
    /// Adds two values and throws <see cref="OverflowException"/> if signed overflow occurs.
    /// </summary>
    public static int256 operator +(int256 left, int256 right)
    {
        int256 result = AddUnchecked(left, right);

        bool signLeft = left.IsNegative;
        bool signRight = right.IsNegative;
        bool signResult = result.IsNegative;

        if (signLeft == signRight && signResult != signLeft)
            throw new OverflowException("int256 addition overflow.");

        return result;
    }

    /// <summary>
    /// Adds two values and wraps on overflow (mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 AddUnchecked(int256 left, int256 right)
    {
        ulong r0 = left._u0 + right._u0;
        ulong c0 = r0 < left._u0 ? 1UL : 0UL;

        ulong r1 = left._u1 + right._u1 + c0;
        ulong c1 = (r1 < left._u1 || (c0 == 1 && r1 == left._u1)) ? 1UL : 0UL;

        ulong r2 = left._u2 + right._u2 + c1;
        ulong c2 = (r2 < left._u2 || (c1 == 1 && r2 == left._u2)) ? 1UL : 0UL;

        ulong r3 = left._u3 + right._u3 + c2;

        return new int256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Subtracts two values and throws <see cref="OverflowException"/> if signed overflow occurs.
    /// </summary>
    public static int256 operator -(int256 left, int256 right)
    {
        int256 result = SubtractUnchecked(left, right);

        bool signLeft = left.IsNegative;
        bool signRight = right.IsNegative;
        bool signResult = result.IsNegative;

        // Overflow if signs differ and result sign differs from left.
        if (signLeft != signRight && signResult != signLeft)
            throw new OverflowException("int256 subtraction overflow.");

        return result;
    }

    /// <summary>
    /// Subtracts two values and wraps on overflow (mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 SubtractUnchecked(int256 left, int256 right)
    {
        ulong b0 = left._u0 < right._u0 ? 1UL : 0UL;
        ulong r0 = left._u0 - right._u0;

        ulong b1 = (left._u1 < right._u1 || (left._u1 == right._u1 && b0 == 1)) ? 1UL : 0UL;
        ulong r1 = left._u1 - right._u1 - b0;

        ulong b2 = (left._u2 < right._u2 || (left._u2 == right._u2 && b1 == 1)) ? 1UL : 0UL;
        ulong r2 = left._u2 - right._u2 - b1;

        ulong r3 = left._u3 - right._u3 - b2;

        return new int256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Negates the value and throws <see cref="OverflowException"/> if the value is <see cref="MinValue"/>.
    /// </summary>
    public static int256 operator -(int256 value)
    {
        if (value.Equals(MinValue))
            throw new OverflowException("int256 negation overflow.");

        return NegateUnchecked(value);
    }

    /// <summary>
    /// Negates the value using two's complement and wraps on overflow (mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 NegateUnchecked(int256 value)
    {
        // Two's complement: ~x + 1
        ulong u0 = ~value._u0;
        ulong u1 = ~value._u1;
        ulong u2 = ~value._u2;
        ulong u3 = ~value._u3;

        ulong r0 = u0 + 1UL;
        ulong c0 = r0 == 0 ? 1UL : 0UL;

        ulong r1 = u1 + c0;
        ulong c1 = (c0 == 1 && r1 == 0) ? 1UL : 0UL;

        ulong r2 = u2 + c1;
        ulong c2 = (c1 == 1 && r2 == 0) ? 1UL : 0UL;

        ulong r3 = u3 + c2;

        return new int256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Multiplies two values (cold-path BigInteger) and throws <see cref="OverflowException"/> if the result exceeds int256 range.
    /// </summary>
    public static int256 operator *(int256 left, int256 right) => MultiplyCold(left, right);

    /// <summary>
    /// Divides two values (cold-path BigInteger) and throws <see cref="DivideByZeroException"/> if divisor is zero.
    /// </summary>
    public static int256 operator /(int256 left, int256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        return DivideCold(left, right);
    }

    /// <summary>
    /// Computes left modulo right (cold-path BigInteger) and throws <see cref="DivideByZeroException"/> if divisor is zero.
    /// </summary>
    public static int256 operator %(int256 left, int256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        return ModuloCold(left, right);
    }

    #endregion

    #region Bitwise and Shifts

    /// <summary>Bitwise AND.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator &(int256 left, int256 right)
        => new int256(left._u0 & right._u0, left._u1 & right._u1, left._u2 & right._u2, left._u3 & right._u3);

    /// <summary>Bitwise OR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator |(int256 left, int256 right)
        => new int256(left._u0 | right._u0, left._u1 | right._u1, left._u2 | right._u2, left._u3 | right._u3);

    /// <summary>Bitwise XOR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator ^(int256 left, int256 right)
        => new int256(left._u0 ^ right._u0, left._u1 ^ right._u1, left._u2 ^ right._u2, left._u3 ^ right._u3);

    /// <summary>Bitwise NOT.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator ~(int256 value)
        => new int256(~value._u0, ~value._u1, ~value._u2, ~value._u3);

    /// <summary>
    /// Shifts the value left by a specified number of bits (logical shift).
    /// </summary>
    public static int256 operator <<(int256 value, int shift)
    {
        shift &= 255;
        if (shift == 0) return value;
        if (shift >= 256) return Zero;

        int wholeLimbs = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;

        if (bitShift == 0)
        {
            return wholeLimbs switch
            {
                0 => value,
                1 => new int256(0, a0, a1, a2),
                2 => new int256(0, 0, a0, a1),
                3 => new int256(0, 0, 0, a0),
                _ => Zero
            };
        }

        int r = 64 - bitShift;

        return wholeLimbs switch
        {
            0 => new int256(
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r),
                (a2 << bitShift) | (a1 >> r),
                (a3 << bitShift) | (a2 >> r)),
            1 => new int256(
                0,
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r),
                (a2 << bitShift) | (a1 >> r)),
            2 => new int256(
                0,
                0,
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r)),
            3 => new int256(
                0,
                0,
                0,
                a0 << bitShift),
            _ => Zero
        };
    }

    /// <summary>
    /// Shifts the value right by a specified number of bits (arithmetic shift with sign extension).
    /// </summary>
    public static int256 operator >>(int256 value, int shift)
    {
        shift &= 255;
        if (shift == 0) return value;

        if (shift >= 256)
            return value.IsNegative ? MinusOne : Zero;

        int wholeLimbs = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;
        ulong fill = value.IsNegative ? ulong.MaxValue : 0UL;

        if (bitShift == 0)
        {
            return wholeLimbs switch
            {
                0 => value,
                1 => new int256(a1, a2, a3, fill),
                2 => new int256(a2, a3, fill, fill),
                3 => new int256(a3, fill, fill, fill),
                _ => value.IsNegative ? MinusOne : Zero
            };
        }

        int r = 64 - bitShift;

        return wholeLimbs switch
        {
            0 => new int256(
                (a0 >> bitShift) | (a1 << r),
                (a1 >> bitShift) | (a2 << r),
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift) | (fill << r)),
            1 => new int256(
                (a1 >> bitShift) | (a2 << r),
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift) | (fill << r),
                fill),
            2 => new int256(
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift) | (fill << r),
                fill,
                fill),
            3 => new int256(
                (a3 >> bitShift) | (fill << r),
                fill,
                fill,
                fill),
            _ => value.IsNegative ? MinusOne : Zero
        };
    }

    #endregion

    #region Conversions

    /// <summary>Implicit conversion from <see cref="long"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(long value) => new(value);

    /// <summary>Explicit conversion to <see cref="long"/> (throws if out of range).</summary>
    public static explicit operator long(int256 value)
    {
        if (value.IsNegative)
        {
            // Must fit in signed 64-bit. Sign extension for limbs must be all 1s and top bits must match.
            if (value._u3 != ulong.MaxValue || value._u2 != ulong.MaxValue || value._u1 != ulong.MaxValue)
                throw new OverflowException("Value is out of range for long.");

            return unchecked((long)value._u0);
        }
        else
        {
            if (value._u3 != 0 || value._u2 != 0 || value._u1 != 0)
                throw new OverflowException("Value is out of range for long.");

            return unchecked((long)value._u0);
        }
    }

    /// <summary>
    /// Converts this value to a signed <see cref="BigInteger"/> (cold-path helper).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public BigInteger ToBigInteger()
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteBigEndian(bytes);
        return new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
    }

    /// <summary>Implicit conversion to <see cref="BigInteger"/> (cold-path).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(int256 value) => value.ToBigInteger();

    /// <summary>
    /// Converts a signed <see cref="BigInteger"/> to <see cref="int256"/> (throws if out of int256 range).
    /// </summary>
    public static explicit operator int256(BigInteger value)
    {
        if (value.IsZero)
            return Zero;

        if (value.Sign > 0)
        {
            if (!TryConvertPositiveBigInteger(value, out int256 pos))
                throw new OverflowException("BigInteger is out of range for int256.");
            return pos;
        }

        // Negative
        if (!TryConvertNegativeBigInteger(value, out int256 neg))
            throw new OverflowException("BigInteger is out of range for int256.");
        return neg;
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into an <see cref="int256"/> using the provided format provider.
    /// Supports:
    /// - Hex with optional "0x" prefix (two's complement if 64 hex digits; otherwise treated as positive magnitude)
    /// - Decimal with optional leading '-' sign
    /// </summary>
    public static int256 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Parses a span into an <see cref="int256"/> using the provided format provider.
    /// </summary>
    public static int256 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out int256 result))
            return result;

        throw new FormatException("Invalid int256 value.");
    }

    /// <summary>
    /// Parses a string into an <see cref="int256"/> using invariant culture.
    /// </summary>
    public static int256 Parse(string s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a span into an <see cref="int256"/> using invariant culture.
    /// </summary>
    public static int256 Parse(ReadOnlySpan<char> s) => Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a string into an <see cref="int256"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out int256 result)
        => TryParse(s.AsSpanSafe(), provider, out result);

    /// <summary>
    /// Tries to parse a span into an <see cref="int256"/> using the provided format provider.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out int256 result)
    {
        s = ByteUtils.TrimWhitespace(s);
        if (s.Length == 0)
        {
            result = Zero;
            return false;
        }

        if (!ByteUtils.TryTrimLeadingSign(s, out bool isNegativePrefix, out ReadOnlySpan<char> unsignedSpan))
        {
            result = Zero;
            return false;
        }

        if (ByteUtils.TryTrimHexPrefix(unsignedSpan, out ReadOnlySpan<char> hexDigits))
        {
            // Accept "0x" as zero.
            if (hexDigits.Length == 0)
            {
                result = Zero;
                return true;
            }

            if (hexDigits.Length > 64)
            {
                result = Zero;
                return false;
            }

            if (!ByteUtils.TryParseUInt256HexChars(hexDigits, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            {
                result = Zero;
                return false;
            }

            if (isNegativePrefix)
            {
                // Interpret as magnitude, then negate into two's complement.
                if (!IsMagnitudeWithinInt256NegativeRange(u0, u1, u2, u3))
                {
                    result = Zero;
                    return false;
                }

                if ((u0 | u1 | u2 | u3) == 0)
                {
                    result = Zero;
                    return true;
                }

                ByteUtils.NegateTwosComplement256(u0, u1, u2, u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3);
                result = new int256(r0, r1, r2, r3);
                return true;
            }

            // No '-' prefix:
            // - If exactly 64 hex digits, interpret as raw two's complement (ABI-like), sign bit may be set.
            // - If shorter, interpret as positive magnitude and require sign bit not set.
            if (hexDigits.Length < 64 && (u3 & 0x8000_0000_0000_0000UL) != 0)
            {
                result = Zero;
                return false; // positive magnitude overflow
            }

            result = new int256(u0, u1, u2, u3);
            return true;
        }

        // Decimal parsing: magnitude parsed as unsigned; apply sign.
        if (!ByteUtils.TryParseUInt256DecimalChars(unsignedSpan, out ulong d0, out ulong d1, out ulong d2, out ulong d3))
        {
            result = Zero;
            return false;
        }

        if (!isNegativePrefix)
        {
            // Positive range: sign bit must be 0.
            if ((d3 & 0x8000_0000_0000_0000UL) != 0)
            {
                result = Zero;
                return false;
            }

            result = new int256(d0, d1, d2, d3);
            return true;
        }

        // Negative range: magnitude must be <= 2^255.
        if (!IsMagnitudeWithinInt256NegativeRange(d0, d1, d2, d3))
        {
            result = Zero;
            return false;
        }

        if ((d0 | d1 | d2 | d3) == 0)
        {
            result = Zero; // allow "-0"
            return true;
        }

        ByteUtils.NegateTwosComplement256(d0, d1, d2, d3, out ulong n0, out ulong n1, out ulong n2, out ulong n3);
        result = new int256(n0, n1, n2, n3);
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded JSON token into an <see cref="int256"/>.
    /// Supports optional surrounding quotes and:
    /// - Hex with optional "0x" prefix
    /// - Decimal with optional leading '-' sign
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out int256 value)
    {
        value = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);

        // Optional surrounding quotes.
        if (utf8.Length >= 2 && utf8[0] == (byte)'"' && utf8[^1] == (byte)'"')
        {
            utf8 = utf8.Slice(1, utf8.Length - 2);
            utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        }

        if (utf8.Length == 0)
            return false;

        if (!ByteUtils.TryTrimLeadingSignUtf8(utf8, out bool isNegativePrefix, out ReadOnlySpan<byte> unsignedSpan))
            return false;

        if (ByteUtils.TryTrimHexPrefixUtf8(unsignedSpan, out ReadOnlySpan<byte> hexDigits))
        {
            if (hexDigits.Length == 0)
            {
                value = Zero;
                return true;
            }

            if (hexDigits.Length > 64)
                return false;

            if (!ByteUtils.TryParseUInt256HexUtf8(hexDigits, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
                return false;

            if (isNegativePrefix)
            {
                if (!IsMagnitudeWithinInt256NegativeRange(u0, u1, u2, u3))
                    return false;

                if ((u0 | u1 | u2 | u3) == 0)
                {
                    value = Zero;
                    return true;
                }

                ByteUtils.NegateTwosComplement256(u0, u1, u2, u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3);
                value = new int256(r0, r1, r2, r3);
                return true;
            }

            if (hexDigits.Length < 64 && (u3 & 0x8000_0000_0000_0000UL) != 0)
                return false;

            value = new int256(u0, u1, u2, u3);
            return true;
        }

        if (!ByteUtils.TryParseUInt256DecimalUtf8(unsignedSpan, out ulong d0, out ulong d1, out ulong d2, out ulong d3))
            return false;

        if (!isNegativePrefix)
        {
            if ((d3 & 0x8000_0000_0000_0000UL) != 0)
                return false;

            value = new int256(d0, d1, d2, d3);
            return true;
        }

        if (!IsMagnitudeWithinInt256NegativeRange(d0, d1, d2, d3))
            return false;

        if ((d0 | d1 | d2 | d3) == 0)
        {
            value = Zero;
            return true;
        }

        ByteUtils.NegateTwosComplement256(d0, d1, d2, d3, out ulong n0, out ulong n1, out ulong n2, out ulong n3);
        value = new int256(n0, n1, n2, n3);
        return true;
    }

    #endregion

    #region Formatting

    #region Decimal Parsing

    /// <summary>
    /// Tries to parse a signed decimal string into an <see cref="int256"/> without throwing.
    /// Accepts an optional leading '+' or '-' sign and ignores leading/trailing whitespace.
    /// </summary>
    /// <param name="s">The input string containing a signed decimal value.</param>
    /// <param name="result">The parsed <see cref="int256"/> if successful; otherwise <see cref="Zero"/>.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDecimal([NotNullWhen(true)] string? s, out int256 result)
    {
        if (s is null)
        {
            result = Zero;
            return false;
        }

        return TryParseDecimal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a signed decimal span into an <see cref="int256"/> without throwing.
    /// Accepts an optional leading '+' or '-' sign and ignores leading/trailing whitespace.
    /// </summary>
    /// <param name="s">The input span containing a signed decimal value.</param>
    /// <param name="result">The parsed <see cref="int256"/> if successful; otherwise <see cref="Zero"/>.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParseDecimal(ReadOnlySpan<char> s, out int256 result)
    {
        result = Zero;

        s = ByteUtils.TrimWhitespace(s);
        if (s.Length == 0)
            return false;

        if (!ByteUtils.TryTrimLeadingSign(s, out bool isNegativePrefix, out ReadOnlySpan<char> unsignedSpan))
            return false;

        unsignedSpan = ByteUtils.TrimWhitespace(unsignedSpan);
        if (unsignedSpan.Length == 0)
            return false;

        if (!ByteUtils.TryParseUInt256DecimalChars(unsignedSpan, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            return false;

        if (!isNegativePrefix)
        {
            // Positive int256 must be <= 2^255 - 1 (sign bit must be 0).
            if ((u3 & 0x8000_0000_0000_0000UL) != 0)
                return false;

            result = new int256(u0, u1, u2, u3);
            return true;
        }

        // Negative magnitude must be <= 2^255 (allow exactly 2^255 -> MinValue).
        if (!IsMagnitudeWithinInt256NegativeRange(u0, u1, u2, u3))
            return false;

        // Allow "-0" as 0.
        if ((u0 | u1 | u2 | u3) == 0)
        {
            result = Zero;
            return true;
        }

        ByteUtils.NegateTwosComplement256(u0, u1, u2, u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3);
        result = new int256(r0, r1, r2, r3);
        return true;
    }

    #endregion

    /// <summary>
    /// Returns the default string representation (decimal, signed).
    /// </summary>
    public override string ToString() => ToString("D", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats the value as a string using:
    /// - "D" / "d": signed decimal (cold-path BigInteger conversion)
    /// - "0x" / "0X": hex with prefix (positive: minimal magnitude, negative: full 64-digit two's complement)
    /// - "x" / "X": hex without prefix (positive: minimal magnitude, negative: full 64-digit two's complement)
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "D";

        Span<char> buffer = stackalloc char[80];
        if (!TryFormat(buffer, out int written, format.AsSpan(), formatProvider))
            throw new FormatException($"Unable to format int256 using format '{format}'.");

        return new string(buffer.Slice(0, written));
    }

    /// <summary>
    /// Attempts to format the value into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        charsWritten = 0;

        if (format.Length == 0)
            format = "D".AsSpan();

        bool decimalFormat = format.Length == 1 && (format[0] == 'd' || format[0] == 'D');
        if (decimalFormat)
        {
            // Cold path: decimal formatting uses BigInteger conversion.
            return ToBigInteger().TryFormat(destination, out charsWritten, default, provider);
        }

        bool withPrefix = format.Length == 2 && format[0] == '0' && (format[1] == 'x' || format[1] == 'X');
        bool uppercase = (format.Length == 1 && format[0] == 'X') || (format.Length == 2 && format[1] == 'X');
        bool hexNoPrefixLower = format.Length == 1 && format[0] == 'x';
        bool hexNoPrefixUpper = format.Length == 1 && format[0] == 'X';

        if (!(withPrefix || hexNoPrefixLower || hexNoPrefixUpper))
            return false;

        int pos = 0;
        if (withPrefix)
        {
            if (destination.Length < 2)
                return false;

            destination[pos++] = '0';
            destination[pos++] = uppercase ? 'X' : 'x';
        }

        if (IsNegative)
        {
            // Negative hex is emitted as full-width two's complement to avoid ambiguity.
            if (destination.Length - pos < 64)
                return false;

            ByteUtils.WriteHexUInt64(destination.Slice(pos + 0, 16), _u3, uppercase);
            ByteUtils.WriteHexUInt64(destination.Slice(pos + 16, 16), _u2, uppercase);
            ByteUtils.WriteHexUInt64(destination.Slice(pos + 32, 16), _u1, uppercase);
            ByteUtils.WriteHexUInt64(destination.Slice(pos + 48, 16), _u0, uppercase);

            charsWritten = pos + 64;
            return true;
        }

        // Positive: minimal hex, grouped by limbs like uint256.
        ReadOnlySpan<char> fmtVar = uppercase ? "X" : "x";
        ReadOnlySpan<char> fmt16 = uppercase ? "X16" : "x16";

        if (_u3 == 0 && _u2 == 0 && _u1 == 0)
        {
            if (!_u0.TryFormat(destination.Slice(pos), out int w0, fmtVar, provider))
                return false;

            charsWritten = pos + w0;
            return true;
        }

        if (_u3 == 0 && _u2 == 0)
        {
            if (!_u1.TryFormat(destination.Slice(pos), out int w1, fmtVar, provider))
                return false;
            pos += w1;

            if (!_u0.TryFormat(destination.Slice(pos), out int w0, fmt16, provider))
                return false;

            charsWritten = pos + w0;
            return true;
        }

        if (_u3 == 0)
        {
            if (!_u2.TryFormat(destination.Slice(pos), out int w2, fmtVar, provider))
                return false;
            pos += w2;

            if (!_u1.TryFormat(destination.Slice(pos), out int w1, fmt16, provider))
                return false;
            pos += w1;

            if (!_u0.TryFormat(destination.Slice(pos), out int w0, fmt16, provider))
                return false;

            charsWritten = pos + w0;
            return true;
        }

        if (!_u3.TryFormat(destination.Slice(pos), out int w3, fmtVar, provider))
            return false;
        pos += w3;

        if (!_u2.TryFormat(destination.Slice(pos), out int w2f, fmt16, provider))
            return false;
        pos += w2f;

        if (!_u1.TryFormat(destination.Slice(pos), out int w1f, fmt16, provider))
            return false;
        pos += w1f;

        if (!_u0.TryFormat(destination.Slice(pos), out int w0f, fmt16, provider))
            return false;

        charsWritten = pos + w0f;
        return true;
    }

    /// <summary>
    /// Attempts to format the value into a UTF-8 destination span.
    /// Output is ASCII-compatible for supported formats.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        Span<char> buffer = stackalloc char[80];
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

        for (int i = 0; i < charsWritten; i++)
            utf8Destination[i] = (byte)buffer[i];

        bytesWritten = charsWritten;
        return true;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Returns true if an unsigned magnitude (u0..u3) is within the allowed magnitude range for negative int256:
    /// magnitude must be <= 2^255 (0x8000..00).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMagnitudeWithinInt256NegativeRange(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        const ulong LimitHi = 0x8000_0000_0000_0000UL;

        if (u3 < LimitHi)
            return true;

        if (u3 > LimitHi)
            return false;

        // If u3 == 0x8000.. then remaining limbs must be 0 for exactly 2^255.
        return (u2 | u1 | u0) == 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int256 MultiplyCold(int256 left, int256 right)
    {
        BigInteger r = (BigInteger)left * (BigInteger)right;
        return (int256)r;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int256 DivideCold(int256 left, int256 right)
    {
        BigInteger q = (BigInteger)left / (BigInteger)right;
        return (int256)q;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int256 ModuloCold(int256 left, int256 right)
    {
        BigInteger r = (BigInteger)left % (BigInteger)right;
        return (int256)r;
    }

    private static bool TryConvertPositiveBigInteger(BigInteger value, out int256 result)
    {
        // value > 0
        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > 32)
        {
            result = Zero;
            return false;
        }

        Span<byte> bytes = stackalloc byte[32];
        bytes.Clear();

        if (!value.TryWriteBytes(bytes.Slice(32 - byteCount), out _, isUnsigned: true, isBigEndian: true))
        {
            result = Zero;
            return false;
        }

        // Positive range: MSB must not set (<= 2^255 - 1).
        if ((bytes[0] & 0x80) != 0)
        {
            result = Zero;
            return false;
        }

        result = new int256(bytes);
        return true;
    }

    private static bool TryConvertNegativeBigInteger(BigInteger value, out int256 result)
    {
        // value < 0
        BigInteger magnitude = BigInteger.Negate(value); // positive
        int byteCount = magnitude.GetByteCount(isUnsigned: true);
        if (byteCount > 32)
        {
            result = Zero;
            return false;
        }

        Span<byte> bytes = stackalloc byte[32];
        bytes.Clear();

        if (!magnitude.TryWriteBytes(bytes.Slice(32 - byteCount), out _, isUnsigned: true, isBigEndian: true))
        {
            result = Zero;
            return false;
        }

        // Negative magnitude range: magnitude must be <= 2^255 exactly.
        // If MSB > 0x80 => overflow. If MSB == 0x80 then all remaining bytes must be zero.
        if (bytes[0] > 0x80)
        {
            result = Zero;
            return false;
        }

        if (bytes[0] == 0x80)
        {
            for (int i = 1; i < 32; i++)
            {
                if (bytes[i] != 0)
                {
                    result = Zero;
                    return false;
                }
            }
        }

        // Convert magnitude bytes to limbs, then negate into two's complement.
        ulong u3 = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        ulong u2 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16));
        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24));

        if ((u0 | u1 | u2 | u3) == 0)
        {
            result = Zero;
            return true;
        }

        ByteUtils.NegateTwosComplement256(u0, u1, u2, u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3);
        result = new int256(r0, r1, r2, r3);
        return true;
    }

    #endregion
}