using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Utils.Uint256;

/// <summary>
/// Allocation-free arithmetic helpers used by hot-path parsing and scaling.
/// </summary>

internal static class UInt256Math
{
    /// <summary>
    /// Multiplies a <see cref="uint256"/> by a <see cref="ulong"/>, returning the low 256 bits and a carry-out.
    /// </summary>
    /// <param name="value">Multiplicand.</param>
    /// <param name="multiplier">Multiplier.</param>
    /// <param name="carryOut">Carry-out beyond 256 bits; non-zero indicates overflow.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 MulU64(in uint256 value, ulong multiplier, out ulong carryOut)
    {
        UInt128 p0 = (UInt128)value.Limb0 * multiplier;
        ulong r0 = (ulong)p0;
        ulong c0 = (ulong)(p0 >> 64);

        UInt128 p1 = (UInt128)value.Limb1 * multiplier + c0;
        ulong r1 = (ulong)p1;
        ulong c1 = (ulong)(p1 >> 64);

        UInt128 p2 = (UInt128)value.Limb2 * multiplier + c1;
        ulong r2 = (ulong)p2;
        ulong c2 = (ulong)(p2 >> 64);

        UInt128 p3 = (UInt128)value.Limb3 * multiplier + c2;
        ulong r3 = (ulong)p3;
        carryOut = (ulong)(p3 >> 64);

        return new uint256(r0, r1, r2, r3);
    }

    /// <summary>
    /// Divides a <see cref="uint256"/> by a <see cref="ulong"/>, returning quotient and remainder.
    /// </summary>
    /// <param name="value">Dividend.</param>
    /// <param name="divisor">Divisor (must be non-zero).</param>
    /// <param name="remainder">Remainder.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint256 DivRemU64(in uint256 value, ulong divisor, out ulong remainder)
    {
        UInt128 r = 0;

        r = (r << 64) | value.Limb3;
        ulong q3 = (ulong)(r / divisor);
        r %= divisor;

        r = (r << 64) | value.Limb2;
        ulong q2 = (ulong)(r / divisor);
        r %= divisor;

        r = (r << 64) | value.Limb1;
        ulong q1 = (ulong)(r / divisor);
        r %= divisor;

        r = (r << 64) | value.Limb0;
        ulong q0 = (ulong)(r / divisor);
        r %= divisor;

        remainder = (ulong)r;
        return new uint256(q0, q1, q2, q3);
    }

    /// <summary>
    /// Multiplies by 10 and adds a digit (0..9) with overflow detection.
    /// </summary>
    /// <param name="value">Value to update in-place.</param>
    /// <param name="digit">Digit 0..9.</param>
    /// <returns>True on success; false if overflow occurs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryMul10AddDigit(ref uint256 value, byte digit)
    {
        value = MulU64(value, 10, out ulong carry);
        if (carry != 0) return false;

        // Add digit.
        ulong u0 = value.Limb0 + digit;
        ulong c0 = (u0 < value.Limb0) ? 1UL : 0UL;

        ulong u1 = value.Limb1 + c0;
        ulong c1 = (c0 != 0 && u1 == 0) ? 1UL : 0UL;

        ulong u2 = value.Limb2 + c1;
        ulong c2 = (c1 != 0 && u2 == 0) ? 1UL : 0UL;

        ulong u3 = value.Limb3 + c2;
        if (c2 != 0 && u3 == 0) return false;

        value = new uint256(u0, u1, u2, u3);
        return true;
    }
}
