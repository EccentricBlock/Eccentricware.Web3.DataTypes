using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;
using EccentricWare.Web3.DataTypes.JsonConverters;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A 256-bit unsigned integer optimized for EVM and Solana blockchain operations.
/// Uses 4 x 64-bit unsigned integers for minimal memory footprint (32 bytes).
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
    // Store as 4 x ulong (little-endian: u0 is least significant)
    private readonly ulong _u0; // bits 0-63 (least significant)
    private readonly ulong _u1; // bits 64-127
    private readonly ulong _u2; // bits 128-191
    private readonly ulong _u3; // bits 192-255 (most significant)

    public static readonly uint256 Zero;
    public static readonly uint256 One = new(1);
    public static readonly uint256 MaxValue = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    #region Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong value)
    {
        _u0 = value;
        _u1 = 0;
        _u2 = 0;
        _u3 = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256(UInt128 low, UInt128 high)
    {
        _u0 = (ulong)low;
        _u1 = (ulong)(low >> 64);
        _u2 = (ulong)high;
        _u3 = (ulong)(high >> 64);
    }

    /// <summary>
    /// Creates a uint256 from a big-endian byte array (up to 32 bytes).
    /// Shorter arrays are left-padded with zeros. Compatible with EVM minimal encoding.
    /// </summary>
    public uint256(ReadOnlySpan<byte> bigEndianBytes)
    {
        if (bigEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes", nameof(bigEndianBytes));

        if (bigEndianBytes.Length == 32)
        {
            // Fast path for exactly 32 bytes
            _u3 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes);
            _u2 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(8));
            _u1 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(16));
            _u0 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(24));
        }
        else
        {
            // Pad shorter arrays
            Span<byte> padded = stackalloc byte[32];
            padded.Clear();
            bigEndianBytes.CopyTo(padded.Slice(32 - bigEndianBytes.Length));
            
            _u3 = BinaryPrimitives.ReadUInt64BigEndian(padded);
            _u2 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(8));
            _u1 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(16));
            _u0 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(24));
        }
    }

    /// <summary>
    /// Creates a uint256 from a little-endian byte array (up to 32 bytes).
    /// Shorter arrays are right-padded with zeros. Compatible with Solana encoding.
    /// </summary>
    public static uint256 FromLittleEndian(ReadOnlySpan<byte> littleEndianBytes)
    {
        if (littleEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes", nameof(littleEndianBytes));

        if (littleEndianBytes.Length == 32)
        {
            // Fast path for exactly 32 bytes
            ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes);
            ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(8));
            ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(16));
            ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(24));
            return new uint256(u0, u1, u2, u3);
        }
        else
        {
            // Pad shorter arrays with zeros on the right (high bytes)
            Span<byte> padded = stackalloc byte[32];
            padded.Clear();
            littleEndianBytes.CopyTo(padded);
            
            ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(padded);
            ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(8));
            ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(16));
            ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(24));
            return new uint256(u0, u1, u2, u3);
        }
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the value as a 32-byte big-endian array.
    /// Compatible with EVM and Solana ABI encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes", nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u3);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u0);
    }

    /// <summary>
    /// Writes the value as a 32-byte little-endian array.
    /// Compatible with Solana native encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Returns the value as a 32-byte big-endian array.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        var bytes = new byte[32];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the value as a 32-byte little-endian array.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        var bytes = new byte[32];
        WriteLittleEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the minimal big-endian byte representation (no leading zeros).
    /// </summary>
    public byte[] ToMinimalBigEndianBytes()
    {
        int byteCount = GetByteCount();
        if (byteCount == 0) return [0];
        
        var bytes = new byte[byteCount];
        Span<byte> full = stackalloc byte[32];
        WriteBigEndian(full);
        full.Slice(32 - byteCount).CopyTo(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets the number of bytes needed to represent this value.
    /// </summary>
    public int GetByteCount()
    {
        if (_u3 != 0) return 24 + GetByteCount(_u3);
        if (_u2 != 0) return 16 + GetByteCount(_u2);
        if (_u1 != 0) return 8 + GetByteCount(_u1);
        return GetByteCount(_u0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetByteCount(ulong value)
    {
        if (value == 0) return 0;
        return (64 - BitOperations.LeadingZeroCount(value) + 7) / 8;
    }

    #endregion

    #region Equality (SIMD Optimized)

    /// <summary>
    /// Compares this value for equality with another.
    /// Uses SIMD when available for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(uint256 other)
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

    public override bool Equals(object? obj) => obj is uint256 other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(uint256 left, uint256 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(uint256 left, uint256 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Numeric comparison (most significant to least significant).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(uint256 other)
    {
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is uint256 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(uint256)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(uint256 left, uint256 right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(uint256 left, uint256 right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(uint256 left, uint256 right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(uint256 left, uint256 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Arithmetic Operators

    /// <summary>
    /// Addition with overflow check.
    /// </summary>
    public static uint256 operator +(uint256 left, uint256 right)
    {
        ulong u0 = left._u0 + right._u0;
        ulong carry0 = u0 < left._u0 ? 1UL : 0UL;

        ulong u1 = left._u1 + right._u1 + carry0;
        ulong carry1 = (u1 < left._u1 || (carry0 == 1 && u1 == left._u1)) ? 1UL : 0UL;

        ulong u2 = left._u2 + right._u2 + carry1;
        ulong carry2 = (u2 < left._u2 || (carry1 == 1 && u2 == left._u2)) ? 1UL : 0UL;

        ulong u3 = left._u3 + right._u3 + carry2;
        
        // Check for overflow (carry out of u3)
        if (u3 < left._u3 || (carry2 == 1 && u3 == left._u3))
            throw new OverflowException("uint256 addition overflow");

        return new uint256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Addition without overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 AddUnchecked(uint256 left, uint256 right)
    {
        ulong u0 = left._u0 + right._u0;
        ulong carry0 = u0 < left._u0 ? 1UL : 0UL;

        ulong u1 = left._u1 + right._u1 + carry0;
        ulong carry1 = (u1 < left._u1 || (carry0 == 1 && u1 == left._u1)) ? 1UL : 0UL;

        ulong u2 = left._u2 + right._u2 + carry1;
        ulong carry2 = (u2 < left._u2 || (carry1 == 1 && u2 == left._u2)) ? 1UL : 0UL;

        ulong u3 = left._u3 + right._u3 + carry2;

        return new uint256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Subtraction with underflow check.
    /// </summary>
    public static uint256 operator -(uint256 left, uint256 right)
    {
        if (left < right)
            throw new OverflowException("uint256 subtraction underflow");

        return SubtractUnchecked(left, right);
    }

    /// <summary>
    /// Subtraction without underflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 SubtractUnchecked(uint256 left, uint256 right)
    {
        ulong borrow0 = left._u0 < right._u0 ? 1UL : 0UL;
        ulong u0 = left._u0 - right._u0;

        ulong borrow1 = (left._u1 < right._u1 || (left._u1 == right._u1 && borrow0 == 1)) ? 1UL : 0UL;
        ulong u1 = left._u1 - right._u1 - borrow0;

        ulong borrow2 = (left._u2 < right._u2 || (left._u2 == right._u2 && borrow1 == 1)) ? 1UL : 0UL;
        ulong u2 = left._u2 - right._u2 - borrow1;

        ulong u3 = left._u3 - right._u3 - borrow2;

        return new uint256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Multiplication with overflow check.
    /// </summary>
    public static uint256 operator *(uint256 left, uint256 right)
    {
        // Check for overflow: if both have bits in upper half, result will overflow
        if ((left._u3 != 0 || left._u2 != 0) && (right._u3 != 0 || right._u2 != 0))
            throw new OverflowException("uint256 multiplication overflow");

        // Use UInt128 for intermediate results
        UInt128 leftLow = new UInt128(left._u1, left._u0);
        UInt128 leftHigh = new UInt128(left._u3, left._u2);
        UInt128 rightLow = new UInt128(right._u1, right._u0);
        UInt128 rightHigh = new UInt128(right._u3, right._u2);

        // Result = leftLow * rightLow + (leftLow * rightHigh + leftHigh * rightLow) << 128
        UInt128 lowResult = leftLow * rightLow;
        UInt128 midResult1 = leftLow * rightHigh;
        UInt128 midResult2 = leftHigh * rightLow;
        
        // Check for overflow in middle terms
        UInt128 midSum = midResult1 + midResult2;
        if (midSum < midResult1) // overflow in mid addition
            throw new OverflowException("uint256 multiplication overflow");

        // The high part is just midSum (shifted 128 bits)
        // Check if midSum would overflow when added
        UInt128 highPart = new UInt128((ulong)(lowResult >> 64), (ulong)lowResult) >> 64 != 0 
            ? throw new OverflowException("uint256 multiplication overflow") 
            : midSum;
        
        // Build result
        ulong r0 = (ulong)lowResult;
        ulong r1 = (ulong)(lowResult >> 64);
        
        // Add midSum to upper 128 bits
        UInt128 upper = new UInt128(r1, 0) + (midSum << 64);
        r1 = (ulong)upper;
        ulong r2 = (ulong)(upper >> 64);
        
        // Need to handle overflow properly - simplified version using BigInteger
        // For production, implement proper 256-bit multiplication
        return (uint256)((BigInteger)left * (BigInteger)right);
    }

    /// <summary>
    /// Division.
    /// </summary>
    public static uint256 operator /(uint256 left, uint256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        // Fast path for small divisors
        if (right._u3 == 0 && right._u2 == 0 && right._u1 == 0)
        {
            return DivideByUlong(left, right._u0);
        }

        // Use BigInteger for general case
        return (uint256)((BigInteger)left / (BigInteger)right);
    }

    /// <summary>
    /// Modulo.
    /// </summary>
    public static uint256 operator %(uint256 left, uint256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();

        // Fast path for small divisors
        if (right._u3 == 0 && right._u2 == 0 && right._u1 == 0)
        {
            return ModuloByUlong(left, right._u0);
        }

        // Use BigInteger for general case
        return (uint256)((BigInteger)left % (BigInteger)right);
    }

    /// <summary>
    /// Division and modulo in one operation.
    /// </summary>
    public static (uint256 Quotient, uint256 Remainder) DivRem(uint256 dividend, uint256 divisor)
    {
        if (divisor.IsZero)
            throw new DivideByZeroException();

        var q = (BigInteger)dividend / (BigInteger)divisor;
        var r = (BigInteger)dividend % (BigInteger)divisor;
        return ((uint256)q, (uint256)r);
    }

    private static uint256 DivideByUlong(uint256 value, ulong divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException();

        UInt128 remainder = 0;
        
        remainder = (remainder << 64) | value._u3;
        ulong q3 = (ulong)(remainder / divisor);
        remainder %= divisor;
        
        remainder = (remainder << 64) | value._u2;
        ulong q2 = (ulong)(remainder / divisor);
        remainder %= divisor;
        
        remainder = (remainder << 64) | value._u1;
        ulong q1 = (ulong)(remainder / divisor);
        remainder %= divisor;
        
        remainder = (remainder << 64) | value._u0;
        ulong q0 = (ulong)(remainder / divisor);

        return new uint256(q0, q1, q2, q3);
    }

    private static uint256 ModuloByUlong(uint256 value, ulong divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException();

        UInt128 remainder = 0;
        remainder = ((remainder << 64) | value._u3) % divisor;
        remainder = ((remainder << 64) | value._u2) % divisor;
        remainder = ((remainder << 64) | value._u1) % divisor;
        remainder = ((remainder << 64) | value._u0) % divisor;

        return new uint256((ulong)remainder);
    }

    /// <summary>
    /// Unary negation (two's complement - wraps around).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator -(uint256 value)
    {
        return ~value + One;
    }

    /// <summary>
    /// Increment.
    /// </summary>
    public static uint256 operator ++(uint256 value) => value + One;

    /// <summary>
    /// Decrement.
    /// </summary>
    public static uint256 operator --(uint256 value) => value - One;

    #endregion

    #region Bitwise Operators (SIMD Optimized)

    /// <summary>
    /// Bitwise AND. Uses SIMD when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator &(uint256 left, uint256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft & vRight;
            return new uint256(result[0], result[1], result[2], result[3]);
        }
        return new uint256(
            left._u0 & right._u0,
            left._u1 & right._u1,
            left._u2 & right._u2,
            left._u3 & right._u3);
    }

    /// <summary>
    /// Bitwise OR. Uses SIMD when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator |(uint256 left, uint256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft | vRight;
            return new uint256(result[0], result[1], result[2], result[3]);
        }
        return new uint256(
            left._u0 | right._u0,
            left._u1 | right._u1,
            left._u2 | right._u2,
            left._u3 | right._u3);
    }

    /// <summary>
    /// Bitwise XOR. Uses SIMD when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator ^(uint256 left, uint256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft ^ vRight;
            return new uint256(result[0], result[1], result[2], result[3]);
        }
        return new uint256(
            left._u0 ^ right._u0,
            left._u1 ^ right._u1,
            left._u2 ^ right._u2,
            left._u3 ^ right._u3);
    }

    /// <summary>
    /// Bitwise NOT. Uses SIMD when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 operator ~(uint256 value)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var v = Vector256.Create(value._u0, value._u1, value._u2, value._u3);
            var result = ~v;
            return new uint256(result[0], result[1], result[2], result[3]);
        }
        return new uint256(~value._u0, ~value._u1, ~value._u2, ~value._u3);
    }

    public static uint256 operator <<(uint256 value, int shift)
    {
        shift &= 255; // Mask to valid range

        if (shift == 0) return value;
        if (shift >= 256) return Zero;

        if (shift >= 192)
        {
            return new uint256(0, 0, 0, value._u0 << (shift - 192));
        }
        if (shift >= 128)
        {
            int s = shift - 128;
            return new uint256(
                0, 0,
                value._u0 << s,
                (value._u1 << s) | (value._u0 >> (64 - s)));
        }
        if (shift >= 64)
        {
            int s = shift - 64;
            return new uint256(
                0,
                value._u0 << s,
                (value._u1 << s) | (value._u0 >> (64 - s)),
                (value._u2 << s) | (value._u1 >> (64 - s)));
        }

        return new uint256(
            value._u0 << shift,
            (value._u1 << shift) | (value._u0 >> (64 - shift)),
            (value._u2 << shift) | (value._u1 >> (64 - shift)),
            (value._u3 << shift) | (value._u2 >> (64 - shift)));
    }

    public static uint256 operator >>(uint256 value, int shift)
    {
        shift &= 255; // Mask to valid range

        if (shift == 0) return value;
        if (shift >= 256) return Zero;

        if (shift >= 192)
        {
            return new uint256(value._u3 >> (shift - 192), 0, 0, 0);
        }
        if (shift >= 128)
        {
            int s = shift - 128;
            return new uint256(
                (value._u2 >> s) | (value._u3 << (64 - s)),
                value._u3 >> s,
                0, 0);
        }
        if (shift >= 64)
        {
            int s = shift - 64;
            return new uint256(
                (value._u1 >> s) | (value._u2 << (64 - s)),
                (value._u2 >> s) | (value._u3 << (64 - s)),
                value._u3 >> s,
                0);
        }

        return new uint256(
            (value._u0 >> shift) | (value._u1 << (64 - shift)),
            (value._u1 >> shift) | (value._u2 << (64 - shift)),
            (value._u2 >> shift) | (value._u3 << (64 - shift)),
            value._u3 >> shift);
    }

    /// <summary>
    /// Returns the number of leading zero bits.
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
    /// Returns the number of trailing zero bits.
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
    /// Returns the number of set bits (population count).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
    {
        return BitOperations.PopCount(_u0) + BitOperations.PopCount(_u1) +
               BitOperations.PopCount(_u2) + BitOperations.PopCount(_u3);
    }

    #endregion

    #region Conversions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(ulong value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(uint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(int value)
    {
        if (value < 0)
            throw new OverflowException("Cannot convert negative value to uint256");
        return new uint256((ulong)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(long value)
    {
        if (value < 0)
            throw new OverflowException("Cannot convert negative value to uint256");
        return new uint256((ulong)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint256(UInt128 value) => new(value, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ulong(uint256 value)
    {
        if (value._u1 != 0 || value._u2 != 0 || value._u3 != 0)
            throw new OverflowException("Value is too large for ulong");
        return value._u0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator UInt128(uint256 value)
    {
        if (value._u2 != 0 || value._u3 != 0)
            throw new OverflowException("Value is too large for UInt128");
        return new UInt128(value._u1, value._u0);
    }

    /// <summary>
    /// Converts uint256 to BigInteger (always safe, no data loss).
    /// Stack-allocated for performance.
    /// </summary>
    public BigInteger ToBigInteger()
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteBigEndian(bytes);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Implicit conversion to BigInteger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(uint256 value) => value.ToBigInteger();

    /// <summary>
    /// Converts BigInteger to uint256.
    /// Throws OverflowException if value is negative or exceeds 256 bits.
    /// </summary>
    public static explicit operator uint256(BigInteger value)
    {
        if (value.Sign < 0)
            throw new OverflowException("Cannot convert negative BigInteger to uint256");

        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > 32)
            throw new OverflowException("BigInteger value exceeds 256 bits");

        Span<byte> bytes = stackalloc byte[32];
        bytes.Clear();
        
        if (!value.TryWriteBytes(bytes.Slice(32 - byteCount), out _, isUnsigned: true, isBigEndian: true))
            throw new OverflowException("Failed to convert BigInteger to uint256");

        return new uint256(bytes);
    }

    /// <summary>
    /// Converts uint256 to HexBigInteger (always safe, no data loss).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator HexBigInteger(uint256 value)
    {
        return new HexBigInteger(value.ToBigInteger());
    }

    /// <summary>
    /// Converts HexBigInteger to uint256.
    /// Throws OverflowException if value is negative or exceeds 256 bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint256(HexBigInteger value)
    {
        return (uint256)value.Value;
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hexadecimal string (with or without 0x prefix).
    /// Allocation-free using span-based parsing.
    /// </summary>
    public static uint256 Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return Zero;

        if (hex.Length > 64)
            throw new FormatException("Hex string too long for uint256");

        // Pad to 64 characters
        Span<char> padded = stackalloc char[64];
        padded.Fill('0');
        hex.CopyTo(padded.Slice(64 - hex.Length));

        // Allocation-free parsing using span overloads
        if (!ulong.TryParse(padded.Slice(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u3))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(padded.Slice(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u2))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(padded.Slice(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u1))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(padded.Slice(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u0))
            throw new FormatException("Invalid hex string");

        return new uint256(u0, u1, u2, u3);
    }

    public static uint256 Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    public static uint256 Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    public static uint256 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hexadecimal string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out uint256 result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return true; // Empty is zero

        if (hex.Length > 64)
            return false;

        // Pad to 64 characters
        Span<char> padded = stackalloc char[64];
        padded.Fill('0');
        hex.CopyTo(padded.Slice(64 - hex.Length));

        // Allocation-free parsing
        if (!ulong.TryParse(padded.Slice(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u3))
            return false;
        if (!ulong.TryParse(padded.Slice(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u2))
            return false;
        if (!ulong.TryParse(padded.Slice(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u1))
            return false;
        if (!ulong.TryParse(padded.Slice(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u0))
            return false;

        result = new uint256(u0, u1, u2, u3);
        return true;
    }

    public static bool TryParse(string? hex, out uint256 result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out uint256 result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out uint256 result)
        => TryParse(s, out result);

    /// <summary>
    /// Parses a decimal string.
    /// </summary>
    public static uint256 ParseDecimal(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
            return Zero;

        // Use BigInteger for decimal parsing, then convert
        if (!BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bigInt))
            throw new FormatException("Invalid decimal string");

        return (uint256)bigInt;
    }

    /// <summary>
    /// Tries to parse a decimal string.
    /// </summary>
    public static bool TryParseDecimal(ReadOnlySpan<char> value, out uint256 result)
    {
        result = Zero;

        if (value.Length == 0)
            return true;

        if (!BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bigInt))
            return false;

        if (bigInt.Sign < 0 || bigInt.GetByteCount(isUnsigned: true) > 32)
            return false;

        result = (uint256)bigInt;
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
    /// "x" or "X" for hex without prefix, "0x" for hex with prefix (default).
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        if (format == "0x" || format == "0X")
        {
            if (_u3 == 0 && _u2 == 0 && _u1 == 0)
                return $"0x{_u0:x}";
            if (_u3 == 0 && _u2 == 0)
                return $"0x{_u1:x}{_u0:x16}";
            if (_u3 == 0)
                return $"0x{_u2:x}{_u1:x16}{_u0:x16}";
            return $"0x{_u3:x}{_u2:x16}{_u1:x16}{_u0:x16}";
        }

        if (format == "x")
        {
            if (_u3 == 0 && _u2 == 0 && _u1 == 0)
                return _u0.ToString("x", CultureInfo.InvariantCulture);
            if (_u3 == 0 && _u2 == 0)
                return $"{_u1:x}{_u0:x16}";
            if (_u3 == 0)
                return $"{_u2:x}{_u1:x16}{_u0:x16}";
            return $"{_u3:x}{_u2:x16}{_u1:x16}{_u0:x16}";
        }

        if (format == "X")
        {
            if (_u3 == 0 && _u2 == 0 && _u1 == 0)
                return _u0.ToString("X", CultureInfo.InvariantCulture);
            if (_u3 == 0 && _u2 == 0)
                return $"{_u1:X}{_u0:X16}";
            if (_u3 == 0)
                return $"{_u2:X}{_u1:X16}{_u0:X16}";
            return $"{_u3:X}{_u2:X16}{_u1:X16}{_u0:X16}";
        }

        if (format == "D" || format == "d")
        {
            return ToBigInteger().ToString(CultureInfo.InvariantCulture);
        }

        throw new FormatException($"Unknown format: {format}");
    }

    /// <summary>
    /// Tries to format the value into the destination span.
    /// Zero-allocation implementation that writes directly to the destination.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        charsWritten = 0;
        bool uppercase = format.Length == 1 && format[0] == 'X';
        bool withPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool isDecimal = format.Length == 1 && (format[0] == 'D' || format[0] == 'd');

        if (isDecimal)
        {
            // Decimal format - use BigInteger
            return ToBigInteger().TryFormat(destination, out charsWritten, default, provider);
        }

        int pos = 0;

        // Write prefix if needed
        if (withPrefix)
        {
            if (destination.Length < 2)
                return false;
            destination[pos++] = '0';
            destination[pos++] = 'x';
        }

        // Format based on which components are non-zero
        ReadOnlySpan<char> hexFormat = uppercase ? "X" : "x";
        ReadOnlySpan<char> hexFormat16 = uppercase ? "X16" : "x16";

        if (_u3 == 0 && _u2 == 0 && _u1 == 0)
        {
            if (!_u0.TryFormat(destination.Slice(pos), out int written, hexFormat, provider))
                return false;
            charsWritten = pos + written;
            return true;
        }

        if (_u3 == 0 && _u2 == 0)
        {
            if (!_u1.TryFormat(destination.Slice(pos), out int w1, hexFormat, provider))
                return false;
            pos += w1;
            if (!_u0.TryFormat(destination.Slice(pos), out int w0, hexFormat16, provider))
                return false;
            charsWritten = pos + w0;
            return true;
        }

        if (_u3 == 0)
        {
            if (!_u2.TryFormat(destination.Slice(pos), out int w2, hexFormat, provider))
                return false;
            pos += w2;
            if (!_u1.TryFormat(destination.Slice(pos), out int w1, hexFormat16, provider))
                return false;
            pos += w1;
            if (!_u0.TryFormat(destination.Slice(pos), out int w0, hexFormat16, provider))
                return false;
            charsWritten = pos + w0;
            return true;
        }

        if (!_u3.TryFormat(destination.Slice(pos), out int written3, hexFormat, provider))
            return false;
        pos += written3;
        if (!_u2.TryFormat(destination.Slice(pos), out int written2, hexFormat16, provider))
            return false;
        pos += written2;
        if (!_u1.TryFormat(destination.Slice(pos), out int written1, hexFormat16, provider))
            return false;
        pos += written1;
        if (!_u0.TryFormat(destination.Slice(pos), out int written0, hexFormat16, provider))
            return false;
        charsWritten = pos + written0;
        return true;
    }

    /// <summary>
    /// Tries to format the value into the destination UTF-8 byte span.
    /// Zero heap allocation implementation using stack buffer.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        // Max length: 78 decimal digits (log10(2^256) ≈ 77.06) or "0x" + 64 hex digits
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

        // All output characters are ASCII, so direct byte conversion is safe
        for (int i = 0; i < charsWritten; i++)
        {
            utf8Destination[i] = (byte)buffer[i];
        }
        bytesWritten = charsWritten;
        return true;
    }

    /// <summary>
    /// Returns the full 64-character hexadecimal representation (no 0x prefix).
    /// </summary>
    public string ToFullHexString() => $"{_u3:x16}{_u2:x16}{_u1:x16}{_u0:x16}";

    /// <summary>
    /// Returns the decimal string representation.
    /// </summary>
    public string ToDecimalString() => ToBigInteger().ToString(CultureInfo.InvariantCulture);

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if this value is zero.
    /// Uses SIMD when available for maximum performance.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Vector256.IsHardwareAccelerated)
            {
                var value = Vector256.Create(_u0, _u1, _u2, _u3);
                return value.Equals(Vector256<ulong>.Zero);
            }
            return (_u0 | _u1 | _u2 | _u3) == 0;
        }
    }

    public bool IsOne
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0 == 1 && _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Returns true if value fits in a ulong.
    /// </summary>
    public bool FitsInUlong
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Returns true if value fits in a UInt128.
    /// </summary>
    public bool FitsInUInt128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Gets the lower 128 bits as UInt128.
    /// </summary>
    public UInt128 Low128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new UInt128(_u1, _u0);
    }

    /// <summary>
    /// Gets the upper 128 bits as UInt128.
    /// </summary>
    public UInt128 High128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new UInt128(_u3, _u2);
    }

    #endregion

    #region EVM/Solana Helpers

    /// <summary>
    /// Returns the value as a 32-byte ABI-encoded value (big-endian, left-padded).
    /// Standard EVM ABI encoding for uint256.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToAbiEncoded() => ToBigEndianBytes();

    /// <summary>
    /// Parses an ABI-encoded uint256 value.
    /// </summary>
    public static uint256 FromAbiEncoded(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32)
            throw new ArgumentException("ABI encoded uint256 requires 32 bytes", nameof(data));
        return new uint256(data.Slice(0, 32));
    }

    /// <summary>
    /// Converts to EVM-style minimal hex string (no leading zeros, lowercase).
    /// </summary>
    public string ToEvmHex() => ToString("0x", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts to a value in wei from a value in ether (multiplies by 10^18).
    /// </summary>
    public static uint256 FromEther(decimal ether)
    {
        if (ether < 0)
            throw new ArgumentException("Value cannot be negative", nameof(ether));
        
        var wei = ether * 1_000_000_000_000_000_000m;
        return (uint256)new BigInteger(wei);
    }

    /// <summary>
    /// Converts to a value in lamports from a value in SOL (multiplies by 10^9).
    /// </summary>
    public static uint256 FromSol(decimal sol)
    {
        if (sol < 0)
            throw new ArgumentException("Value cannot be negative", nameof(sol));
        
        var lamports = sol * 1_000_000_000m;
        return (uint256)new BigInteger(lamports);
    }

    /// <summary>
    /// Common EVM constants
    /// </summary>
    public static class Evm
    {
        public static readonly uint256 WeiPerEther = Parse("0xde0b6b3a7640000", CultureInfo.InvariantCulture); // 10^18
        public static readonly uint256 WeiPerGwei = new(1_000_000_000UL); // 10^9
    }

    /// <summary>
    /// Common Solana constants
    /// </summary>
    public static class Solana
    {
        public static readonly uint256 LamportsPerSol = new(1_000_000_000UL); // 10^9
    }

    #endregion
}
