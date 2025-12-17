using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A 256-bit signed integer optimized for EVM and Solana blockchain operations.
/// Uses 4 x 64-bit unsigned integers with two's complement representation.
/// Memory layout: 32 bytes (4 x ulong).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Int256JsonConverter))]
public readonly struct int256 : 
    IEquatable<int256>, 
    IComparable<int256>, 
    IComparable,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<int256>
{
    // Store as 4 x ulong (little-endian: u0 is least significant)
    // Two's complement: sign bit is bit 255 (MSB of _u3)
    private readonly ulong _u0; // bits 0-63 (least significant)
    private readonly ulong _u1; // bits 64-127
    private readonly ulong _u2; // bits 128-191
    private readonly ulong _u3; // bits 192-255 (most significant, contains sign bit)

    /// <summary>
    /// The value 0.
    /// </summary>
    public static readonly int256 Zero;

    /// <summary>
    /// The value 1.
    /// </summary>
    public static readonly int256 One = new(1, 0, 0, 0);

    /// <summary>
    /// The value -1 (all bits set).
    /// </summary>
    public static readonly int256 MinusOne = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    /// <summary>
    /// The maximum value (2^255 - 1).
    /// </summary>
    public static readonly int256 MaxValue = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, long.MaxValue);

    /// <summary>
    /// The minimum value (-2^255).
    /// </summary>
    public static readonly int256 MinValue = new(0, 0, 0, unchecked((ulong)long.MinValue));

    #region Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256(long value)
    {
        _u0 = (ulong)value;
        // Sign-extend: if negative, fill upper bits with 1s
        ulong signExtend = value < 0 ? ulong.MaxValue : 0;
        _u1 = signExtend;
        _u2 = signExtend;
        _u3 = signExtend;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256(Int128 low, Int128 high)
    {
        _u0 = (ulong)low;
        _u1 = (ulong)((UInt128)low >> 64);
        _u2 = (ulong)high;
        _u3 = (ulong)((UInt128)high >> 64);
    }

    /// <summary>
    /// Creates an int256 from a big-endian byte array (up to 32 bytes).
    /// Interprets as signed two's complement.
    /// </summary>
    public int256(ReadOnlySpan<byte> bigEndianBytes)
    {
        if (bigEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes", nameof(bigEndianBytes));

        if (bigEndianBytes.Length == 32)
        {
            _u3 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes);
            _u2 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(8));
            _u1 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(16));
            _u0 = BinaryPrimitives.ReadUInt64BigEndian(bigEndianBytes.Slice(24));
        }
        else
        {
            // Sign-extend shorter arrays
            bool negative = bigEndianBytes.Length > 0 && (bigEndianBytes[0] & 0x80) != 0;
            Span<byte> padded = stackalloc byte[32];
            padded.Fill(negative ? (byte)0xFF : (byte)0);
            bigEndianBytes.CopyTo(padded.Slice(32 - bigEndianBytes.Length));
            
            _u3 = BinaryPrimitives.ReadUInt64BigEndian(padded);
            _u2 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(8));
            _u1 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(16));
            _u0 = BinaryPrimitives.ReadUInt64BigEndian(padded.Slice(24));
        }
    }

    /// <summary>
    /// Creates an int256 from a little-endian byte array (up to 32 bytes).
    /// </summary>
    public static int256 FromLittleEndian(ReadOnlySpan<byte> littleEndianBytes)
    {
        if (littleEndianBytes.Length > 32)
            throw new ArgumentException("Expected at most 32 bytes", nameof(littleEndianBytes));

        if (littleEndianBytes.Length == 32)
        {
            ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes);
            ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(8));
            ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(16));
            ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(littleEndianBytes.Slice(24));
            return new int256(u0, u1, u2, u3);
        }
        else
        {
            // Sign-extend shorter arrays
            bool negative = littleEndianBytes.Length > 0 && (littleEndianBytes[^1] & 0x80) != 0;
            Span<byte> padded = stackalloc byte[32];
            padded.Fill(negative ? (byte)0xFF : (byte)0);
            littleEndianBytes.CopyTo(padded);
            
            ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(padded);
            ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(8));
            ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(16));
            ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(padded.Slice(24));
            return new int256(u0, u1, u2, u3);
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if this value is negative.
    /// </summary>
    public bool IsNegative
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ((long)_u3) < 0;
    }

    /// <summary>
    /// Returns the sign of this value: -1, 0, or 1.
    /// </summary>
    public int Sign
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (IsZero) return 0;
            return IsNegative ? -1 : 1;
        }
    }

    /// <summary>
    /// Returns true if this value is zero.
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

    /// <summary>
    /// Returns true if this value is one.
    /// </summary>
    public bool IsOne
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0 == 1 && _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    /// <summary>
    /// Returns true if value fits in a long.
    /// </summary>
    public bool FitsInLong
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (IsNegative)
            {
                // All upper bits must be 1, and _u0 must be valid negative long
                return _u1 == ulong.MaxValue && _u2 == ulong.MaxValue && _u3 == ulong.MaxValue
                       && (long)_u0 < 0;
            }
            // All upper bits must be 0, and _u0 must be valid positive long
            return _u1 == 0 && _u2 == 0 && _u3 == 0 && (long)_u0 >= 0;
        }
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the value as a 32-byte big-endian array.
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

    #endregion

    #region Equality (SIMD Optimized)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(int256 other)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var left = Vector256.Create(_u0, _u1, _u2, _u3);
            var right = Vector256.Create(other._u0, other._u1, other._u2, other._u3);
            return left.Equals(right);
        }
        return _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;
    }

    public override bool Equals(object? obj) => obj is int256 other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(int256 left, int256 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(int256 left, int256 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Signed comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(int256 other)
    {
        // Compare signs first
        bool thisNeg = IsNegative;
        bool otherNeg = other.IsNegative;
        
        if (thisNeg != otherNeg)
            return thisNeg ? -1 : 1;

        // Same sign: compare magnitudes (from MSB to LSB)
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is int256 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(int256)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(int256 left, int256 right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(int256 left, int256 right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(int256 left, int256 right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(int256 left, int256 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Arithmetic Operators

    /// <summary>
    /// Addition (wrapping, no overflow check for signed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator +(int256 left, int256 right)
    {
        ulong u0 = left._u0 + right._u0;
        ulong carry0 = u0 < left._u0 ? 1UL : 0UL;

        ulong u1 = left._u1 + right._u1 + carry0;
        ulong carry1 = (u1 < left._u1 || (carry0 == 1 && u1 == left._u1)) ? 1UL : 0UL;

        ulong u2 = left._u2 + right._u2 + carry1;
        ulong carry2 = (u2 < left._u2 || (carry1 == 1 && u2 == left._u2)) ? 1UL : 0UL;

        ulong u3 = left._u3 + right._u3 + carry2;

        return new int256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Subtraction (wrapping).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator -(int256 left, int256 right)
    {
        ulong borrow0 = left._u0 < right._u0 ? 1UL : 0UL;
        ulong u0 = left._u0 - right._u0;

        ulong borrow1 = (left._u1 < right._u1 || (left._u1 == right._u1 && borrow0 == 1)) ? 1UL : 0UL;
        ulong u1 = left._u1 - right._u1 - borrow0;

        ulong borrow2 = (left._u2 < right._u2 || (left._u2 == right._u2 && borrow1 == 1)) ? 1UL : 0UL;
        ulong u2 = left._u2 - right._u2 - borrow1;

        ulong u3 = left._u3 - right._u3 - borrow2;

        return new int256(u0, u1, u2, u3);
    }

    /// <summary>
    /// Unary negation (two's complement).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator -(int256 value)
    {
        return ~value + One;
    }

    /// <summary>
    /// Unary plus (identity).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator +(int256 value) => value;

    /// <summary>
    /// Multiplication using BigInteger for correctness.
    /// </summary>
    public static int256 operator *(int256 left, int256 right)
    {
        return (int256)((BigInteger)left * (BigInteger)right);
    }

    /// <summary>
    /// Division using BigInteger for correctness.
    /// </summary>
    public static int256 operator /(int256 left, int256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();
        return (int256)((BigInteger)left / (BigInteger)right);
    }

    /// <summary>
    /// Modulo using BigInteger for correctness.
    /// </summary>
    public static int256 operator %(int256 left, int256 right)
    {
        if (right.IsZero)
            throw new DivideByZeroException();
        return (int256)((BigInteger)left % (BigInteger)right);
    }

    /// <summary>
    /// Increment.
    /// </summary>
    public static int256 operator ++(int256 value) => value + One;

    /// <summary>
    /// Decrement.
    /// </summary>
    public static int256 operator --(int256 value) => value - One;

    /// <summary>
    /// Returns the absolute value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int256 Abs() => IsNegative ? -this : this;

    #endregion

    #region Bitwise Operators (SIMD Optimized)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator &(int256 left, int256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft & vRight;
            return new int256(result[0], result[1], result[2], result[3]);
        }
        return new int256(
            left._u0 & right._u0,
            left._u1 & right._u1,
            left._u2 & right._u2,
            left._u3 & right._u3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator |(int256 left, int256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft | vRight;
            return new int256(result[0], result[1], result[2], result[3]);
        }
        return new int256(
            left._u0 | right._u0,
            left._u1 | right._u1,
            left._u2 | right._u2,
            left._u3 | right._u3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator ^(int256 left, int256 right)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var vLeft = Vector256.Create(left._u0, left._u1, left._u2, left._u3);
            var vRight = Vector256.Create(right._u0, right._u1, right._u2, right._u3);
            var result = vLeft ^ vRight;
            return new int256(result[0], result[1], result[2], result[3]);
        }
        return new int256(
            left._u0 ^ right._u0,
            left._u1 ^ right._u1,
            left._u2 ^ right._u2,
            left._u3 ^ right._u3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int256 operator ~(int256 value)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var v = Vector256.Create(value._u0, value._u1, value._u2, value._u3);
            var result = ~v;
            return new int256(result[0], result[1], result[2], result[3]);
        }
        return new int256(~value._u0, ~value._u1, ~value._u2, ~value._u3);
    }

    /// <summary>
    /// Left shift.
    /// </summary>
    public static int256 operator <<(int256 value, int shift)
    {
        shift &= 255;
        if (shift == 0) return value;
        if (shift >= 256) return Zero;

        if (shift >= 192)
        {
            return new int256(0, 0, 0, value._u0 << (shift - 192));
        }
        if (shift >= 128)
        {
            int s = shift - 128;
            return new int256(
                0, 0,
                value._u0 << s,
                (value._u1 << s) | (value._u0 >> (64 - s)));
        }
        if (shift >= 64)
        {
            int s = shift - 64;
            return new int256(
                0,
                value._u0 << s,
                (value._u1 << s) | (value._u0 >> (64 - s)),
                (value._u2 << s) | (value._u1 >> (64 - s)));
        }

        return new int256(
            value._u0 << shift,
            (value._u1 << shift) | (value._u0 >> (64 - shift)),
            (value._u2 << shift) | (value._u1 >> (64 - shift)),
            (value._u3 << shift) | (value._u2 >> (64 - shift)));
    }

    /// <summary>
    /// Arithmetic right shift (sign-extending).
    /// </summary>
    public static int256 operator >>(int256 value, int shift)
    {
        shift &= 255;
        if (shift == 0) return value;

        // Sign-extended fill value
        ulong fill = value.IsNegative ? ulong.MaxValue : 0;

        if (shift >= 256)
            return new int256(fill, fill, fill, fill);

        if (shift >= 192)
        {
            int s = shift - 192;
            // Arithmetic shift the MSB
            return new int256(
                ((long)value._u3 >> s) < 0 ? (value._u3 >> s) | (ulong.MaxValue << (64 - s)) : value._u3 >> s,
                fill, fill, fill);
        }
        if (shift >= 128)
        {
            int s = shift - 128;
            return new int256(
                (value._u2 >> s) | (value._u3 << (64 - s)),
                (ulong)(((long)value._u3) >> s), // Arithmetic shift
                fill, fill);
        }
        if (shift >= 64)
        {
            int s = shift - 64;
            return new int256(
                (value._u1 >> s) | (value._u2 << (64 - s)),
                (value._u2 >> s) | (value._u3 << (64 - s)),
                (ulong)(((long)value._u3) >> s),
                fill);
        }

        return new int256(
            (value._u0 >> shift) | (value._u1 << (64 - shift)),
            (value._u1 >> shift) | (value._u2 << (64 - shift)),
            (value._u2 >> shift) | (value._u3 << (64 - shift)),
            (ulong)((long)value._u3 >> shift));
    }

    #endregion

    #region Conversions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(long value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(int value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(short value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(sbyte value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int256(ulong value) => value > (ulong)long.MaxValue
    ? throw new OverflowException("Value too large for implicit conversion")
    : new int256(value, 0, 0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator long(int256 value)
    {
        if (!value.FitsInLong)
            throw new OverflowException("Value does not fit in long");
        return (long)value._u0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int256(uint256 value)
    {
        // Check if MSB is set (would be negative as int256)
        Span<byte> bytes = stackalloc byte[32];
        value.WriteBigEndian(bytes);
        if ((bytes[0] & 0x80) != 0)
            throw new OverflowException("uint256 value would be negative as int256");
        return new int256(bytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint256(int256 value)
    {
        if (value.IsNegative)
            throw new OverflowException("Cannot convert negative int256 to uint256");
        return new uint256(value._u0, value._u1, value._u2, value._u3);
    }

    /// <summary>
    /// Converts to BigInteger.
    /// </summary>
    public BigInteger ToBigInteger()
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteBigEndian(bytes);
        return new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BigInteger(int256 value) => value.ToBigInteger();

    /// <summary>
    /// Converts BigInteger to int256.
    /// </summary>
    public static explicit operator int256(BigInteger value)
    {
        int byteCount = value.GetByteCount(isUnsigned: false);
        if (byteCount > 32)
            throw new OverflowException("BigInteger value exceeds 256 bits");

        Span<byte> bytes = stackalloc byte[32];
        
        // Sign-extend
        if (value.Sign < 0)
            bytes.Fill(0xFF);
        else
            bytes.Clear();

        if (!value.TryWriteBytes(bytes.Slice(32 - byteCount), out _, isUnsigned: false, isBigEndian: true))
            throw new OverflowException("Failed to convert BigInteger to int256");

        return new int256(bytes);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a hexadecimal string (with or without 0x prefix).
    /// Supports negative values with leading '-'.
    /// </summary>

    public static int256 Parse(ReadOnlySpan<byte> utf8)
    {
        if (!TryParse(utf8, out var value))
            throw new FormatException("Invalid int256 JSON-RPC value");
        return value;
    }

    public static int256 Parse(ReadOnlySpan<char> hex)
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

        if (hex.Length > 64)
            throw new FormatException("Hex string too long for int256");

        // Parse as unsigned first
        if (!uint256.TryParse(hex, out var unsigned))
            throw new FormatException("Invalid hex string");

        var result = new int256(unsigned._GetU0(), unsigned._GetU1(), unsigned._GetU2(), unsigned._GetU3());
        return negative ? -result : result;
    }

    public static int256 Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    public static int256 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
        {
            return result;
        }

        throw new FormatException("Invalid hexadecimal string");
    }
    public static int256 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Tries to parse a hexadecimal string without exceptions.
    /// </summary>

    public static bool TryParse(ReadOnlySpan<byte> utf8, out int256 value)
    {
        value = Zero;

        if (utf8.Length == 0)
            return true;

        // Trim surrounding quotes if present
        if (utf8.Length >= 2 && utf8[0] == (byte)'"' && utf8[^1] == (byte)'"')
            utf8 = utf8.Slice(1, utf8.Length - 2);

        if (utf8.Length == 0)
            return true;

        // Detect sign
        bool negative = false;
        if (utf8[0] == (byte)'-')
        {
            negative = true;
            utf8 = utf8.Slice(1);
            if (utf8.Length == 0)
                return false;
        }

        // ---------- EVM hex path ----------
        if (utf8.Length >= 2 && utf8[0] == (byte)'0' && (utf8[1] | 0x20) == (byte)'x')
        {
            utf8 = utf8.Slice(2);
            if (utf8.Length == 0)
            {
                value = Zero;
                return true;
            }

            if (utf8.Length > 64)
                return false;

            ulong u0 = 0, u1 = 0, u2 = 0, u3 = 0;

            int limb = 0;
            ulong acc = 0;
            int shift = 0;

            // Parse from least-significant nibble
            for (int i = utf8.Length - 1; i >= 0; i--)
            {
                int n = ByteUtils.ParseHexNibbleUtf8(utf8[i]);
                if (n < 0)
                    return false;

                acc |= (ulong)n << shift;
                shift += 4;

                if (shift == 64)
                {
                    switch (limb)
                    {
                        case 0: u0 = acc; break;
                        case 1: u1 = acc; break;
                        case 2: u2 = acc; break;
                        case 3: u3 = acc; break;
                        default: return false;
                    }
                    limb++;
                    acc = 0;
                    shift = 0;
                }
            }

            if (shift != 0)
            {
                switch (limb)
                {
                    case 0: u0 = acc; break;
                    case 1: u1 = acc; break;
                    case 2: u2 = acc; break;
                    case 3: u3 = acc; break;
                    default: return false;
                }
            }

            var result = new int256(u0, u1, u2, u3);
            value = negative ? -result : result;
            return true;
        }

        // ---------- Decimal path (Solana + generic JSON-RPC) ----------
        BigInteger big = BigInteger.Zero;

        for (int i = 0; i < utf8.Length; i++)
        {
            byte c = utf8[i];
            if ((uint)(c - '0') > 9)
                return false;

            big = big * 10 + (c - '0');
            if (big.GetByteCount(isUnsigned: false) > 32)
                return false;
        }

        if (negative)
            big = -big;

        value = (int256)big;
        return true;
    }


    public static bool TryParse(ReadOnlySpan<char> hex, out int256 result)
    {
        result = Zero;

        bool negative = false;
        if (hex.Length > 0 && hex[0] == '-')
        {
            negative = true;
            hex = hex.Slice(1);
        }

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == 0)
            return true;

        if (hex.Length > 64)
            return false;

        if (!uint256.TryParse(hex, out var unsigned))
            return false;

        result = new int256(unsigned._GetU0(), unsigned._GetU1(), unsigned._GetU2(), unsigned._GetU3());
        if (negative)
            result = -result;
        return true;
    }

    public static bool TryParse(string? hex, out int256 result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out int256 result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out int256 result)
        => TryParse(s, out result);

    /// <summary>
    /// Parses a decimal string.
    /// </summary>
    public static int256 ParseDecimal(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
            return Zero;

        if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var bigInt))
            throw new FormatException("Invalid decimal string");

        return (int256)bigInt;
    }

    /// <summary>
    /// Tries to parse a decimal string.
    /// </summary>
    public static bool TryParseDecimal(ReadOnlySpan<char> value, out int256 result)
    {
        result = Zero;

        if (value.Length == 0)
            return true;

        if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var bigInt))
            return false;

        if (bigInt.GetByteCount(isUnsigned: false) > 32)
            return false;

        result = (int256)bigInt;
        return true;
    }

    #endregion

    #region Formatting

    public override string ToString() => ToString(null, null);

    /// <summary>
    /// Formats the value according to the format string.
    /// "x" or "X" for hex without prefix, "0x" for hex with prefix (default), "d" for decimal.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        if (format == "D" || format == "d")
        {
            return ToBigInteger().ToString(CultureInfo.InvariantCulture);
        }

        bool withPrefix = format == "0x" || format == "0X";
        bool uppercase = format == "X" || format == "0X";

        if (IsNegative)
        {
            var abs = Abs();
            string hex = abs.ToHexStringInternal(uppercase);
            return withPrefix ? "-0x" + hex : "-" + hex;
        }

        string hexStr = ToHexStringInternal(uppercase);
        return withPrefix ? "0x" + hexStr : hexStr;
    }

    private string ToHexStringInternal(bool uppercase)
    {
        if (IsZero) return "0";

        string fmt = uppercase ? "X" : "x";
        string fmt16 = uppercase ? "X16" : "x16";

        if (_u3 == 0 && _u2 == 0 && _u1 == 0)
            return _u0.ToString(fmt, CultureInfo.InvariantCulture);
        if (_u3 == 0 && _u2 == 0)
            return $"{_u1.ToString(fmt, CultureInfo.InvariantCulture)}{_u0.ToString(fmt16, CultureInfo.InvariantCulture)}";
        if (_u3 == 0)
            return $"{_u2.ToString(fmt, CultureInfo.InvariantCulture)}{_u1.ToString(fmt16, CultureInfo.InvariantCulture)}{_u0.ToString(fmt16, CultureInfo.InvariantCulture)}";
        return $"{_u3.ToString(fmt, CultureInfo.InvariantCulture)}{_u2.ToString(fmt16, CultureInfo.InvariantCulture)}{_u1.ToString(fmt16, CultureInfo.InvariantCulture)}{_u0.ToString(fmt16, CultureInfo.InvariantCulture)}";
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        string str = ToString(format.Length == 0 ? null : new string(format), provider);
        if (str.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        str.AsSpan().CopyTo(destination);
        charsWritten = str.Length;
        return true;
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        Span<char> buffer = stackalloc char[80]; // Max: "-0x" + 64 hex
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
    /// Returns the decimal string representation.
    /// </summary>
    public string ToDecimalString() => ToBigInteger().ToString(CultureInfo.InvariantCulture);

    #endregion

    #region EVM/Solana Helpers

    /// <summary>
    /// Returns the value as a 32-byte ABI-encoded value (big-endian, two's complement).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToAbiEncoded() => ToBigEndianBytes();

    /// <summary>
    /// Parses an ABI-encoded int256 value.
    /// </summary>
    public static int256 FromAbiEncoded(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32)
            throw new ArgumentException("ABI encoded int256 requires 32 bytes", nameof(data));
        return new int256(data.Slice(0, 32));
    }

    /// <summary>
    /// Converts to EVM-style minimal hex string.
    /// </summary>
    public string ToEvmHex() => ToString("0x", CultureInfo.InvariantCulture);

    #endregion
}

// Extension methods to access uint256 internals for int256 construction
internal static class UInt256Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong _GetU0(this uint256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.WriteLittleEndian(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong _GetU1(this uint256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.WriteLittleEndian(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong _GetU2(this uint256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.WriteLittleEndian(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong _GetU3(this uint256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.WriteLittleEndian(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(24));
    }
}

