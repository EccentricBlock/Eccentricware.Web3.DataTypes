using System.Numerics;
using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// Slow-path members for <see cref="uint256"/>.
/// </summary>
public readonly partial struct uint256
{
    /// <summary>
    /// Converts the value to a <see cref="BigInteger"/> (unsigned).
    /// </summary>
    /// <remarks>
    /// Metadata tags: [slowpath] [bigint]
    /// Allocates due to <see cref="BigInteger"/> internals.
    /// </remarks>
    public BigInteger ToBigInteger()
    {
        Span<byte> bytes = stackalloc byte[32];
        WriteBigEndian(bytes);
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Creates a <see cref="uint256"/> from a non-negative <see cref="BigInteger"/> that fits into 256 bits.
    /// </summary>
    public static uint256 FromBigInteger(BigInteger value)
    {
        if (value.Sign < 0)
            throw new OverflowException("uint256 cannot be negative.");

        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > 32)
            throw new OverflowException("Value does not fit into 256 bits.");

        Span<byte> buffer = stackalloc byte[32];
        buffer.Clear();

        if (!value.TryWriteBytes(buffer.Slice(32 - byteCount), out _, isUnsigned: true, isBigEndian: true))
            throw new OverflowException("Failed to write BigInteger bytes.");

        return FromBigEndian32(buffer);
    }

    /// <summary>
    /// Implicit conversion to <see cref="BigInteger"/> (slow-path).
    /// </summary>
    public static implicit operator BigInteger(uint256 value) => value.ToBigInteger();

    /// <summary>
    /// Explicit conversion from <see cref="BigInteger"/> (slow-path).
    /// </summary>
    public static explicit operator uint256(BigInteger value) => FromBigInteger(value);

    /// <summary>
    /// Default string representation: canonical EVM quantity (lowercase).
    /// </summary>
    public override string ToString()
    {
        // Uses span formatting (hot-path code), allocates only the final string.
        Span<char> tmp = stackalloc char[66];
        if (!TryFormat(tmp, out int charsWritten, "0x".AsSpan(), provider: null))
            return "0x0";

        return new string(tmp.Slice(0, charsWritten));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryScaleUpPow10Slow(byte decimalPlaces, out uint256 scaledValue)
    {
        BigInteger big = ToBigInteger();

        BigInteger pow10 = BigInteger.One;
        for (int i = 0; i < decimalPlaces; i++)
            pow10 *= 10;

        BigInteger scaled = big * pow10;

        try
        {
            scaledValue = FromBigInteger(scaled);
            return true;
        }
        catch (OverflowException)
        {
            scaledValue = Zero;
            return false;
        }
    }
}
