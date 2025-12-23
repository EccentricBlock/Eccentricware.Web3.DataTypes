using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// Represents a 256-bit unsigned integer with a fixed 32-byte in-memory layout (4 x 64-bit limbs).
/// Optimised for Web3 workloads (EVM/Solana), including allocation-free parsing/formatting and efficient hashing for large indices.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(UInt256JsonConverter))]
public readonly struct uint256 :
    IEquatable<uint256>,
    IComparable<uint256>,
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<uint256>
{
    /// <summary>Least significant 64 bits (bits 0..63).</summary>
    private readonly ulong _u0;
    /// <summary>Bits 64..127.</summary>
    private readonly ulong _u1;
    /// <summary>Bits 128..191.</summary>
    private readonly ulong _u2;
    /// <summary>Most significant 64 bits (bits 192..255).</summary>
    private readonly ulong _u3;

    /// <summary>Represents the value 0.</summary>
    public static readonly uint256 Zero = new uint256(0UL);

    /// <summary>Represents the value 1.</summary>
    public static readonly uint256 One = new uint256(1UL);

    /// <summary>Represents the maximum possible value (2^256 - 1).</summary>
    public static readonly uint256 MaxValue = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    #region Constructors

    /// <summary>
    /// Creates a 256-bit value from a 64-bit unsigned integer.
    /// </summary>
    /// <param name="value">The 64-bit value to place in the least significant limb.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong value)
    {
        _u0 = value;
        _u1 = 0;
        _u2 = 0;
        _u3 = 0;
    }

    /// <summary>
    /// Creates a 256-bit value from four 64-bit limbs (little-endian limb order).
    /// </summary>
    /// <param name="u0">Least significant 64 bits.</param>
    /// <param name="u1">Bits 64..127.</param>
    /// <param name="u2">Bits 128..191.</param>
    /// <param name="u3">Most significant 64 bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    /// <summary>
    /// Creates a 256-bit value from two 128-bit halves.
    /// </summary>
    /// <param name="low">Lower 128 bits.</param>
    /// <param name="high">Upper 128 bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(UInt128 low, UInt128 high)
    {
        _u0 = (ulong)low;
        _u1 = (ulong)(low >> 64);
        _u2 = (ulong)high;
        _u3 = (ulong)(high >> 64);
    }

    /// <summary>
    /// Creates a 256-bit value from a big-endian byte sequence of length 0..32.
    /// Shorter sequences are left-padded with zeros.
    /// </summary>
    /// <param name="bigEndianBytes">The big-endian bytes representing the value.</param>
    public uint256(ReadOnlySpan<byte> bigEndianBytes)
    {
        if (bigEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes.", nameof(bigEndianBytes));

        if (bigEndianBytes.Length == 32)
        {
            _u3 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes);
            _u2 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(8));
            _u1 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(16));
            _u0 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(24));
            return;
        }

        Span<byte> padded = stackalloc byte[32];
        padded.Clear();
        bigEndianBytes.CopyTo(padded.Slice(32 - bigEndianBytes.Length));

        _u3 = BinaryPrimitives.ReadUInt64BigEndian(padded);
        _u2 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(8));
        _u1 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(16));
        _u0 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(24));
    }

    /// <summary>
    /// Creates a 256-bit value from a little-endian byte sequence of length 0..32.
    /// Shorter sequences are right-padded with zeros.
    /// </summary>
    /// <param name="littleEndianBytes">The little-endian bytes representing the value.</param>
    /// <returns>A <see cref="uint256"/> value.</returns>
    public static uint256 FromLittleEndian(ReadOnlySpan<byte> littleEndianBytes)
    {
        if (littleEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes.", nameof(littleEndianBytes));

        if (littleEndianBytes.Length == 32)
        {
            ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes);
            ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(8));
            ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(16));
            ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(24));
            return new uint256(u0, u1, u2, u3);
        }

        Span<byte> padded = stackalloc byte[32];
        padded.Clear();
        littleEndianBytes.CopyTo(padded);

        return new uint256(
            BinaryPrimitives.ReadUInt64LittleEndian(padded),
            BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(8)),
            BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(16)),
            BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(24)));
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
    /// Gets a value indicating whether this value equals one.
    /// </summary>
    public bool IsOne
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0 == 1 && _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Gets a value indicating whether this value fits in a 64-bit unsigned integer.
    /// </summary>
    public bool FitsInUlong
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Gets a value indicating whether this value fits in a 128-bit unsigned integer.
    /// </summary>
    public bool FitsInUInt128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Gets the lower 128 bits of this value.
    /// </summary>
    public UInt128 Low128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new UInt128(_u1, _u0);
    }

    /// <summary>
    /// Gets the upper 128 bits of this value.
    /// </summary>
    public UInt128 High128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new UInt128(_u3, _u2);
    }

    /// <summary>
    /// Gets the least significant 64 bits (often useful for fast paths).
    /// </summary>
    public ulong Low64
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0;
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the value as a 32-byte big-endian representation into the destination span.
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
    /// Writes the value as a 32-byte little-endian representation into the destination span.
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
    /// Returns a new 32-byte big-endian array representing this value.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        byte[] bytes = new byte[32];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns a new 32-byte little-endian array representing this value.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        byte[] bytes = new byte[32];
        WriteLittleEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the minimal big-endian byte array (no leading zeros). Returns {0} for zero.
    /// </summary>
    public byte[] ToMinimalBigEndianBytes()
    {
        int byteCount = GetByteCount();
        if (byteCount == 0)
            return new byte[] { 0 };

        byte[] bytes = new byte[byteCount];
        Span<byte> full = stackalloc byte[32];
        WriteBigEndian(full);
        full.Slice(32 - byteCount).CopyTo(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets the number of bytes required to represent this value (0 for zero).
    /// </summary>
    public int GetByteCount()
    {
        if (_u3 != 0) return 24 + ByteUtils.GetByteCount(_u3);
        if (_u2 != 0) return 16 + ByteUtils.GetByteCount(_u2);
        if (_u1 != 0) return 8 + ByteUtils.GetByteCount(_u1);
        return ByteUtils.GetByteCount(_u0);
    }

    #endregion

    #region Equality and Hashing

    /// <summary>
    /// Determines whether this value equals another value using a branch-light scalar reduction.
    /// </summary>
    /// <param name="other">The other value to compare.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(uint256 other)
    {
        ulong diff = (_u0 ^ other._u0) | (_u1 ^ other._u1) | (_u2 ^ other._u2) | (_u3 ^ other._u3);
        return diff == 0;
    }

    /// <summary>
    /// Determines whether this value equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is uint256 other && Equals(other);

    /// <summary>
    /// Computes a high-quality hash code suitable for very large hash tables (millions of keys).
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
    public static bool operator ==(uint256 left, uint256 right) => left.Equals(right);

    /// <summary>
    /// Determines whether two values are not equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(uint256 left, uint256 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Compares this instance to another <see cref="uint256"/> (numeric comparison).
    /// </summary>
    /// <param name="other">The other value to compare.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(uint256 other)
    {
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        return 0;
    }

    /// <summary>
    /// Compares this instance to another object (numeric comparison).
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is uint256 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(uint256)}.", nameof(obj));
    }

    /// <summary>Returns true if left is less than right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(uint256 left, uint256 right) => left.CompareTo(right) < 0;

    /// <summary>Returns true if left is greater than right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(uint256 left, uint256 right) => left.CompareTo(right) > 0;

    /// <summary>Returns true if left is less than or equal to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(uint256 left, uint256 right) => left.CompareTo(right) <= 0;

    /// <summary>Returns true if left is greater than or equal to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(uint256 left, uint256 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Arithmetic

    /// <summary>
    /// Adds two values and throws <see cref="OverflowException"/> if the result exceeds 256 bits.
    /// </summary>
    public static uint256 operator +(uint256 left, uint256 right)
    {
        ulong r0 = left._u0 + right._u0;
        ulong c0 = r0 < left._u0 ? 1UL : 0UL;

        ulong r1 = left._u1 + right._u1 + c0;
        ulong c1 = (r1 < left._u1 || (c0 == 1 && r1 == left._u1)) ? 1UL : 0UL;

        ulong r2 = left._u2 + right._u2 + c1;
        ulong c2 = (r2 < left._u2 || (c1 == 1 && r2 == left._u2)) ? 1UL : 0UL;

        ulong r3 = left._u3 + right._u3 + c2;
        if (r3 < left._u3 || (c2 == 1 && r3 == left._u3))
            throw new OverflowException("uint256 addition overflow.");

        return new uint256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Adds two values and wraps on overflow (mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 AddUnchecked(uint256 left, uint256 right)
    {
        ulong r0 = left._u0 + right._u0;
        ulong c0 = r0 < left._u0 ? 1UL : 0UL;

        ulong r1 = left._u1 + right._u1 + c0;
        ulong c1 = (r1 < left._u1 || (c0 == 1 && r1 == left._u1)) ? 1UL : 0UL;

        ulong r2 = left._u2 + right._u2 + c1;
        ulong c2 = (r2 < left._u2 || (c1 == 1 && r2 == left._u2)) ? 1UL : 0UL;

        ulong r3 = left._u3 + right._u3 + c2;

        return new uint256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Subtracts two values and throws <see cref="OverflowException"/> if the result would be negative.
    /// </summary>
    public static uint256 operator -(uint256 left, uint256 right)
    {
        if (left < right)
            throw new OverflowException("uint256 subtraction underflow.");

        return SubtractUnchecked(left, right);
    }

    /// <summary>
    /// Subtracts two values and wraps on underflow (mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 SubtractUnchecked(uint256 left, uint256 right)
    {
        ulong b0 = left._u0 < right._u0 ? 1UL : 0UL;
        ulong r0 = left._u0 - right._u0;

        ulong b1 = (left._u1 < right._u1 || (left._u1 == right._u1 && b0 == 1)) ? 1UL : 0UL;
        ulong r1 = left._u1 - right._u1 - b0;

        ulong b2 = (left._u2 < right._u2 || (left._u2 == right._u2 && b1 == 1)) ? 1UL : 0UL;
        ulong r2 = left._u2 - right._u2 - b1;

        ulong r3 = left._u3 - right._u3 - b2;

        return new uint256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Multiplies two values and throws <see cref="OverflowException"/> if the full product exceeds 256 bits.
    /// </summary>
    public static uint256 operator *(uint256 left, uint256 right)
    {
        Span<ulong> r = stackalloc ulong[8];
        Multiply512(left, right, r);

        if ((r[4] | r[5] | r[6] | r[7]) != 0)
            throw new OverflowException("uint256 multiplication overflow.");

        return new uint256(r[0], r[1], r[2], r[3]);
    }

    /// <summary>
    /// Multiplies two values and returns the low 256 bits of the product (wraps mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 MultiplyUnchecked(uint256 left, uint256 right)
    {
        Span<ulong> r = stackalloc ulong[8];
        Multiply512(left, right, r);
        return new uint256(r[0], r[1], r[2], r[3]);
    }

    /// <summary>
    /// Divides two values. Uses a fast path for 64-bit divisors and a cold-path BigInteger fallback otherwise.
    /// </summary>
    public static uint256 operator /(uint256 left, uint256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        if (right._u3 == 0 && right._u2 == 0 && right._u1 == 0)
            return left.DivRem(right._u0, out _);

        return DivideCold(left, right);
    }

    /// <summary>
    /// Computes left modulo right. Uses a fast path for 64-bit divisors and a cold-path BigInteger fallback otherwise.
    /// </summary>
    public static uint256 operator %(uint256 left, uint256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        if (right._u3 == 0 && right._u2 == 0 && right._u1 == 0)
        {
            _ = left.DivRem(right._u0, out ulong rem);
            return new uint256(rem);
        }

        return ModuloCold(left, right);
    }

    /// <summary>
    /// Computes quotient and remainder for a 256-bit dividend and divisor.
    /// Uses a fast path for 64-bit divisors and a cold-path BigInteger fallback otherwise.
    /// </summary>
    public static (uint256 Quotient, uint256 Remainder) DivRem(uint256 dividend, uint256 divisor)
    {
        if (divisor.IsZero)
            throw new DivideByZeroException();

        if (divisor._u3 == 0 && divisor._u2 == 0 && divisor._u1 == 0)
        {
            uint256 q = dividend.DivRem(divisor._u0, out ulong r);
            return (q, new uint256(r));
        }

        return DivRemCold(dividend, divisor);
    }

    /// <summary>
    /// Multiplies this value by a 64-bit factor without allocating and returns false on overflow.
    /// Intended for ERC20 scaling (e.g., multiply by 10^decimals).
    /// </summary>
    /// <param name="factor">The 64-bit factor.</param>
    /// <param name="result">The multiplication result if successful.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryMultiply(ulong factor, out uint256 result)
    {
        if (factor == 0) { result = Zero; return true; }
        if (factor == 1) { result = this; return true; }

        UInt128 carry = 0;

        UInt128 p0 = (UInt128)_u0 * factor + carry;
        ulong r0 = (ulong)p0;
        carry = p0 >> 64;

        UInt128 p1 = (UInt128)_u1 * factor + carry;
        ulong r1 = (ulong)p1;
        carry = p1 >> 64;

        UInt128 p2 = (UInt128)_u2 * factor + carry;
        ulong r2 = (ulong)p2;
        carry = p2 >> 64;

        UInt128 p3 = (UInt128)_u3 * factor + carry;
        ulong r3 = (ulong)p3;
        carry = p3 >> 64;

        if (carry != 0)
        {
            result = Zero;
            return false;
        }

        result = new uint256(r0, r1, r2, r3);
        return true;
    }

    /// <summary>
    /// Divides this value by a 64-bit divisor without allocating and returns the quotient and remainder.
    /// Intended for ERC20 scaling and decimal adjustments.
    /// </summary>
    /// <param name="divisor">The 64-bit divisor.</param>
    /// <param name="remainder">The remainder as a 64-bit value.</param>
    public uint256 DivRem(ulong divisor, out ulong remainder)
    {
        if (divisor == 0)
            throw new DivideByZeroException();

        UInt128 rem = 0;

        rem = (rem << 64) | _u3;
        ulong q3 = (ulong)(rem / divisor);
        rem %= divisor;

        rem = (rem << 64) | _u2;
        ulong q2 = (ulong)(rem / divisor);
        rem %= divisor;

        rem = (rem << 64) | _u1;
        ulong q1 = (ulong)(rem / divisor);
        rem %= divisor;

        rem = (rem << 64) | _u0;
        ulong q0 = (ulong)(rem / divisor);
        rem %= divisor;

        remainder = (ulong)rem;
        return new uint256(q0, q1, q2, q3);
    }

    /// <summary>
    /// Returns the value of 10^exponent for exponent in the range 0..19 (fits in <see cref="ulong"/>).
    /// Useful for ERC20 decimals (typically 0..18).
    /// </summary>
    /// <param name="exponent">The exponent in the range 0..19.</param>
    public static ulong Pow10U64(int exponent)
    {
        if ((uint)exponent > 19u)
            throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be between 0 and 19 (inclusive).");

        return ByteUtils.Pow10U64(exponent);
    }

    /// <summary>
    /// Unary negation using two's complement semantics (wraps mod 2^256).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator -(uint256 value) => AddUnchecked(~value, One);

    /// <summary>Increment (checked).</summary>
    public static uint256 operator ++(uint256 value) => value + One;

    /// <summary>Decrement (checked).</summary>
    public static uint256 operator --(uint256 value) => value - One;

    #endregion

    #region Bitwise and Shifts

    /// <summary>Bitwise AND.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator &(uint256 left, uint256 right)
        => new uint256(left._u0 & right._u0, left._u1 & right._u1, left._u2 & right._u2, left._u3 & right._u3);

    /// <summary>Bitwise OR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator |(uint256 left, uint256 right)
        => new uint256(left._u0 | right._u0, left._u1 | right._u1, left._u2 | right._u2, left._u3 | right._u3);

    /// <summary>Bitwise XOR.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator ^(uint256 left, uint256 right)
        => new uint256(left._u0 ^ right._u0, left._u1 ^ right._u1, left._u2 ^ right._u2, left._u3 ^ right._u3);

    /// <summary>Bitwise NOT.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator ~(uint256 value)
        => new uint256(~value._u0, ~value._u1, ~value._u2, ~value._u3);

    /// <summary>
    /// Shifts the value left by a specified number of bits (logical shift).
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shift">The number of bits to shift (masked to 0..255).</param>
    public static uint256 operator <<(uint256 value, int shift)
    {
        if (shift == 0) return value;
        if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift), "Shift count must be non-negative.");
        if (shift >= 256) return Zero;

        int wholeLimbs = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;

        if (bitShift == 0)
        {
            return wholeLimbs switch
            {
                0 => value,
                1 => new uint256(0, a0, a1, a2),
                2 => new uint256(0, 0, a0, a1),
                3 => new uint256(0, 0, 0, a0),
                _ => Zero
            };
        }

        int r = 64 - bitShift;

        return wholeLimbs switch
        {
            0 => new uint256(
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r),
                (a2 << bitShift) | (a1 >> r),
                (a3 << bitShift) | (a2 >> r)),
            1 => new uint256(
                0,
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r),
                (a2 << bitShift) | (a1 >> r)),
            2 => new uint256(
                0,
                0,
                a0 << bitShift,
                (a1 << bitShift) | (a0 >> r)),
            3 => new uint256(
                0,
                0,
                0,
                a0 << bitShift),
            _ => Zero
        };
    }

    /// <summary>
    /// Shifts the value right by a specified number of bits (logical shift).
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shift">The number of bits to shift (masked to 0..255).</param>
    public static uint256 operator >>(uint256 value, int shift)
    {
        if (shift == 0) return value;
        if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift), "Shift count must be non-negative.");
        if (shift >= 256) return Zero;

        int wholeLimbs = shift >> 6;
        int bitShift = shift & 63;

        ulong a0 = value._u0, a1 = value._u1, a2 = value._u2, a3 = value._u3;

        if (bitShift == 0)
        {
            return wholeLimbs switch
            {
                0 => value,
                1 => new uint256(a1, a2, a3, 0),
                2 => new uint256(a2, a3, 0, 0),
                3 => new uint256(a3, 0, 0, 0),
                _ => Zero
            };
        }

        int r = 64 - bitShift;

        return wholeLimbs switch
        {
            0 => new uint256(
                (a0 >> bitShift) | (a1 << r),
                (a1 >> bitShift) | (a2 << r),
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift)),
            1 => new uint256(
                (a1 >> bitShift) | (a2 << r),
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift),
                0),
            2 => new uint256(
                (a2 >> bitShift) | (a3 << r),
                (a3 >> bitShift),
                0,
                0),
            3 => new uint256(
                (a3 >> bitShift),
                0,
                0,
                0),
            _ => Zero
        };
    }

    /// <summary>
    /// Gets the number of leading zero bits in this value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LeadingZeroCount()
    {
        if (_u3 != 0) return BitOperations.LeadingZeroCount(_u3);
        if (_u2 != 0) return 64 + BitOperations.LeadingZeroCount(_u2);
        if (_u1 != 0) return 128 + BitOperations.LeadingZeroCount(_u1);
        if (_u0 != 0) return 192 + BitOperations.LeadingZeroCount(_u0);
        return 256;
    }

    /// <summary>
    /// Gets the number of trailing zero bits in this value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TrailingZeroCount()
    {
        if (_u0 != 0) return BitOperations.TrailingZeroCount(_u0);
        if (_u1 != 0) return 64 + BitOperations.TrailingZeroCount(_u1);
        if (_u2 != 0) return 128 + BitOperations.TrailingZeroCount(_u2);
        if (_u3 != 0) return 192 + BitOperations.TrailingZeroCount(_u3);
        return 256;
    }

    /// <summary>
    /// Gets the number of set bits in this value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
        => BitOperations.PopCount(_u0) + BitOperations.PopCount(_u1) + BitOperations.PopCount(_u2) + BitOperations.PopCount(_u3);

    #endregion

    #region Conversions

    /// <summary>Implicit conversion from <see cref="ulong"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(ulong value) => new(value);

    /// <summary>Implicit conversion from <see cref="uint"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(uint value) => new((ulong)value);

    /// <summary>Implicit conversion from <see cref="int"/> (throws if negative).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(int value)
    {
        if (value < 0) throw new OverflowException("Cannot convert a negative value to uint256.");
        return new uint256((ulong)value);
    }

    /// <summary>Implicit conversion from <see cref="long"/> (throws if negative).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(long value)
    {
        if (value < 0) throw new OverflowException("Cannot convert a negative value to uint256.");
        return new uint256((ulong)value);
    }

    /// <summary>Implicit conversion from <see cref="UInt128"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(UInt128 value) => new(value, 0);

    /// <summary>Explicit conversion to <see cref="ulong"/> (throws if value does not fit).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ulong(uint256 value)
    {
        if (value._u1 != 0 || value._u2 != 0 || value._u3 != 0)
            throw new OverflowException("Value is too large for ulong.");
        return value._u0;
    }

    /// <summary>Explicit conversion to <see cref="UInt128"/> (throws if value does not fit).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator UInt128(uint256 value)
    {
        if (value._u2 != 0 || value._u3 != 0)
            throw new OverflowException("Value is too large for UInt128.");
        return new UInt128(value._u1, value._u0);
    }

    /// <summary>
    /// Converts this value to a <see cref="BigInteger"/> (cold-path helper).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public BigInteger ToBigInteger()
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteBigEndian(bytes);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>Implicit conversion to <see cref="BigInteger"/> (cold-path).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(uint256 value) => value.ToBigInteger();

    /// <summary>
    /// Converts a <see cref="BigInteger"/> to <see cref="uint256"/> (throws if negative or exceeds 256 bits).
    /// </summary>
    public static explicit operator uint256(BigInteger value)
    {
        if (value.Sign < 0)
            throw new OverflowException("Cannot convert a negative BigInteger to uint256.");

        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > 32)
            throw new OverflowException("BigInteger value exceeds 256 bits.");

        Span<byte> bytes = stackalloc byte[32];
        bytes.Clear();

        if (!value.TryWriteBytes(bytes.Slice(32 - byteCount), out _, isUnsigned: true, isBigEndian: true))
            throw new OverflowException("Failed to convert BigInteger to uint256.");

        return new uint256(bytes);
    }

    /// <summary>
    /// Converts this value to a HexBigInteger (cold-path convenience).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(uint256 value) => new HexBigInteger(value.ToBigInteger());

    /// <summary>
    /// Converts a HexBigInteger to uint256 (throws if negative or exceeds 256 bits).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint256(HexBigInteger value) => (uint256)value.Value;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hexadecimal string (with optional 0x prefix) into a <see cref="uint256"/>.
    /// </summary>
    /// <param name="s">The input span containing the hexadecimal string.</param>
    /// <returns>The parsed <see cref="uint256"/> value.</returns>
    public static uint256 Parse(ReadOnlySpan<char> s)
    {
        if (!TryParse(s, CultureInfo.InvariantCulture, out uint256 result))
            throw new FormatException("Invalid uint256 hexadecimal string.");

        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded JSON-RPC numeric token (0x-prefixed hex or decimal).
    /// Optional surrounding quotes are supported.
    /// </summary>
    /// <param name="utf8">The UTF-8 bytes representing the token.</param>
    public static uint256 Parse(ReadOnlySpan<byte> utf8)
    {
        if (!TryParse(utf8, out uint256 value))
            throw new FormatException("Invalid uint256 JSON-RPC numeric token.");
        return value;
    }

    /// <summary>
    /// Parses a string into a <see cref="uint256"/> using the provided format provider.
    /// </summary>
    public static uint256 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Parses a span into a <see cref="uint256"/> using the provided format provider.
    /// </summary>
    public static uint256 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out uint256 result))
            return result;

        throw new FormatException("Invalid uint256 value.");
    }

    /// <summary>
    /// Parses a string into a <see cref="uint256"/> using the invariant culture.
    /// </summary>
    public static uint256 Parse(string s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hexadecimal string (with optional 0x prefix).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out uint256 result) => TryParse(s, CultureInfo.InvariantCulture, out result);

    /// <summary>
    /// Tries to parse a string into a <see cref="uint256"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out uint256 result)
        => TryParse(s.AsSpanSafe(), CultureInfo.InvariantCulture, out result);

    /// <summary>
    /// Tries to parse a span into a <see cref="uint256"/> using the specified format provider.
    /// This method supports:
    /// - Hex with optional "0x" prefix
    /// - Decimal without prefix
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out uint256 result)
    {
        s = ByteUtils.TrimWhitespace(s);
        if (s.Length == 0)
        {
            result = Zero;
            return false;
        }

        if (ByteUtils.TryTrimHexPrefix(s, out ReadOnlySpan<char> hexDigits))
        {
            // Accept "0x" as zero (common in JSON-RPC).
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

            result = new uint256(u0, u1, u2, u3);
            return true;
        }

        // Decimal path (allocation-free).
        if (!ByteUtils.TryParseUInt256DecimalChars(s, out ulong d0, out ulong d1, out ulong d2, out ulong d3))
        {
            result = Zero;
            return false;
        }

        result = new uint256(d0, d1, d2, d3);
        return true;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="uint256"/> using the specified format provider.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out uint256 result)
        => TryParse(s.AsSpanSafe(), provider, out result);

    /// <summary>
    /// Tries to parse a UTF-8 encoded JSON-RPC numeric token (0x-prefixed hex or decimal).
    /// Optional surrounding quotes are supported.
    /// </summary>
    /// <param name="utf8">The UTF-8 input token bytes.</param>
    /// <param name="value">The parsed value if successful.</param>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out uint256 value)
    {
        value = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);

        // Optional surrounding quotes (JSON-RPC often uses strings for numbers).
        if (utf8.Length >= 2 && utf8[0] == (byte)'"' && utf8[^1] == (byte)'"')
        {
            utf8 = utf8.Slice(1, utf8.Length - 2);
            utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        }

        if (utf8.Length == 0)
            return false;

        if (ByteUtils.TryTrimHexPrefixUtf8(utf8, out ReadOnlySpan<byte> hexDigits))
        {
            // Accept "0x" as zero.
            if (hexDigits.Length == 0)
            {
                value = Zero;
                return true;
            }

            if (hexDigits.Length > 64)
                return false;

            if (!ByteUtils.TryParseUInt256HexUtf8(hexDigits, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
                return false;

            value = new uint256(u0, u1, u2, u3);
            return true;
        }

        if (!ByteUtils.TryParseUInt256DecimalUtf8(utf8, out ulong d0, out ulong d1, out ulong d2, out ulong d3))
            return false;

        value = new uint256(d0, d1, d2, d3);
        return true;
    }

    /// <summary>
    /// Parses a decimal string into a <see cref="uint256"/> without allocating.
    /// </summary>
    /// <param name="value">The decimal digits.</param>
    public static uint256 ParseDecimal(ReadOnlySpan<char> value)
    {
        value = ByteUtils.TrimWhitespace(value);
        if (!ByteUtils.TryParseUInt256DecimalChars(value, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            throw new FormatException("Invalid decimal string for uint256.");

        return new uint256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Tries to parse a decimal string into a <see cref="uint256"/> without allocating.
    /// </summary>
    public static bool TryParseDecimal(ReadOnlySpan<char> value, out uint256 result)
    {
        value = ByteUtils.TrimWhitespace(value);
        if (!ByteUtils.TryParseUInt256DecimalChars(value, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
        {
            result = Zero;
            return false;
        }

        result = new uint256(u0, u1, u2, u3);
        return true;
    }

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the default string representation ("0x" prefixed hex).
    /// </summary>
    public override string ToString() => ToString("0x", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats the value as a string using:
    /// - "0x" or "0X" for hex with prefix (default)
    /// - "x" or "X" for hex without prefix
    /// - "d" or "D" for decimal (cold-path BigInteger conversion)
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        format ??= "0x";

        if (format.Length == 1)
        {
            char f = format[0];
            if (f == 'x' || f == 'X')
            {
                Span<char> buffer = stackalloc char[64];
                _ = TryFormat(buffer, out int written, format.AsSpan(), formatProvider);
                return new string(buffer.Slice(0, written));
            }

            if (f == 'd' || f == 'D')
                return ToBigInteger().ToString(CultureInfo.InvariantCulture);
        }
        else if (format.Length == 2 && (format[0] == '0') && (format[1] == 'x' || format[1] == 'X'))
        {
            Span<char> buffer = stackalloc char[66];
            _ = TryFormat(buffer, out int written, format.AsSpan(), formatProvider);
            return new string(buffer.Slice(0, written));
        }

        throw new FormatException($"Unknown format: {format}");
    }

    /// <summary>
    /// Attempts to format the value into the destination character span.
    /// Supported formats: "0x"/"0X" (default), "x", "X", "d", "D".
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        charsWritten = 0;

        if (format.Length == 0)
            format = "0x".AsSpan();

        bool withPrefix = format.Length == 2 && format[0] == '0' && (format[1] == 'x' || format[1] == 'X');
        bool uppercase = (format.Length == 1 && format[0] == 'X') || (format.Length == 2 && format[1] == 'X');
        bool decimalFormat = format.Length == 1 && (format[0] == 'd' || format[0] == 'D');

        if (decimalFormat)
        {
            // Cold path: decimal formatting uses BigInteger conversion.
            return ToBigInteger().TryFormat(destination, out charsWritten, default, provider);
        }

        int pos = 0;

        if (withPrefix)
        {
            if (destination.Length < 2) return false;
            destination[pos++] = '0';
            destination[pos++] = uppercase ? 'X' : 'x';
        }

        ReadOnlySpan<char> fmtVar = uppercase ? "X" : "x";
        ReadOnlySpan<char> fmt16 = uppercase ? "X16" : "x16";

        // Minimal hex (no leading zeros) while preserving limb grouping.
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

    /// <summary>
    /// Returns a fixed-width 64-character hex string without a prefix (cold-path helper).
    /// </summary>
    public string ToFullHexString()
    {
        Span<char> buffer = stackalloc char[64];
        WriteFullHexTo(buffer, uppercase: false);
        return new string(buffer);
    }

    /// <summary>
    /// Returns a decimal string representation (cold-path helper).
    /// </summary>
    public string ToDecimalString() => ToBigInteger().ToString(CultureInfo.InvariantCulture);

    #endregion

    #region EVM/Solana Helpers

    /// <summary>
    /// Returns a 32-byte ABI-encoded value (big-endian, left-padded).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToAbiEncoded() => ToBigEndianBytes();

    /// <summary>
    /// Creates a value from a 32-byte ABI-encoded representation.
    /// </summary>
    /// <param name="data">The ABI-encoded bytes (must be at least 32 bytes).</param>
    public static uint256 FromAbiEncoded(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32)
            throw new ArgumentException("ABI encoded uint256 requires 32 bytes.", nameof(data));

        return new uint256(data.Slice(0, 32));
    }

    /// <summary>
    /// Converts this value to a minimal EVM-style hex string ("0x" prefix, no leading zeros).
    /// </summary>
    public string ToEvmHex() => ToString("0x", CultureInfo.InvariantCulture);

    /// <summary>
    /// Common EVM constants.
    /// </summary>
    public static class Evm
    {
        /// <summary>Number of wei per ether (10^18).</summary>
        public static readonly uint256 WeiPerEther = Parse("0xde0b6b3a7640000".AsSpan(), CultureInfo.InvariantCulture);

        /// <summary>Number of wei per gwei (10^9).</summary>
        public static readonly uint256 WeiPerGwei = new uint256(1_000_000_000UL);
    }

    /// <summary>
    /// Common Solana constants.
    /// </summary>
    public static class Solana
    {
        /// <summary>Number of lamports per SOL (10^9).</summary>
        public static readonly uint256 LamportsPerSol = new uint256(1_000_000_000UL);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Multiplies two 256-bit values into a 512-bit product (8 x 64-bit limbs).
    /// This method is allocation-free and is used by checked/unchecked multiplication.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="productLimbs">A span of length 8 receiving the product limbs (little-endian limb order).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Multiply512(uint256 left, uint256 right, Span<ulong> productLimbs)
    {
        productLimbs.Clear();

        ulong a0 = left._u0, a1 = left._u1, a2 = left._u2, a3 = left._u3;
        ulong b0 = right._u0, b1 = right._u1, b2 = right._u2, b3 = right._u3;

        MulAddRow(a0, 0, b0, b1, b2, b3, productLimbs);
        MulAddRow(a1, 1, b0, b1, b2, b3, productLimbs);
        MulAddRow(a2, 2, b0, b1, b2, b3, productLimbs);
        MulAddRow(a3, 3, b0, b1, b2, b3, productLimbs);

        static void MulAddRow(ulong a, int row, ulong b0, ulong b1, ulong b2, ulong b3, Span<ulong> r)
        {
            UInt128 carry = 0;

            AddMul(r, row + 0, a, b0, ref carry);
            AddMul(r, row + 1, a, b1, ref carry);
            AddMul(r, row + 2, a, b2, ref carry);
            AddMul(r, row + 3, a, b3, ref carry);

            // Propagate remaining carry into higher limbs.
            int k = row + 4;
            while (carry != 0 && k < 8)
            {
                UInt128 acc = (UInt128)r[k] + (ulong)carry;
                r[k] = (ulong)acc;
                carry = (acc >> 64) + (carry >> 64);
                k++;
            }
        }

        static void AddMul(Span<ulong> r, int k, ulong a, ulong b, ref UInt128 carry)
        {
            UInt128 prod = (UInt128)a * b;
            ulong lo = (ulong)prod;
            ulong hi = (ulong)(prod >> 64);

            UInt128 acc = (UInt128)r[k] + lo + (ulong)carry;
            r[k] = (ulong)acc;

            carry = (acc >> 64) + hi + (carry >> 64);
        }
    }

    /// <summary>
    /// Performs a cold-path division using BigInteger for general divisors.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint256 DivideCold(uint256 left, uint256 right) => (uint256)((BigInteger)left / (BigInteger)right);

    /// <summary>
    /// Performs a cold-path modulo using BigInteger for general divisors.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint256 ModuloCold(uint256 left, uint256 right) => (uint256)((BigInteger)left % (BigInteger)right);

    /// <summary>
    /// Performs a cold-path DivRem using BigInteger for general divisors.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (uint256 Quotient, uint256 Remainder) DivRemCold(uint256 dividend, uint256 divisor)
    {
        BigInteger q = BigInteger.DivRem((BigInteger)dividend, (BigInteger)divisor, out BigInteger r);
        return ((uint256)q, (uint256)r);
    }

    /// <summary>
    /// Writes full-width hex (64 characters) into the destination span.
    /// </summary>
    /// <param name="destination">The destination span (must be exactly 64 chars).</param>
    /// <param name="uppercase">True for uppercase A-F, otherwise lowercase.</param>
    private void WriteFullHexTo(Span<char> destination, bool uppercase)
    {
        if (destination.Length != 64)
            throw new ArgumentException("Destination must be exactly 64 characters.", nameof(destination));

        ByteUtils.WriteHexUInt64(destination.Slice(0, 16), _u3, uppercase);
        ByteUtils.WriteHexUInt64(destination.Slice(16, 16), _u2, uppercase);
        ByteUtils.WriteHexUInt64(destination.Slice(32, 16), _u1, uppercase);
        ByteUtils.WriteHexUInt64(destination.Slice(48, 16), _u0, uppercase);
    }

    #endregion
}