using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Utils.Uint256;

/// <summary>
/// Fast power-of-ten table for common token decimal scaling.
/// </summary>
internal static class UInt256Pow10
{
    // 10^0..10^19 fits into ulong.
    private static ReadOnlySpan<ulong> Pow10U64 => new ulong[]
    {
        1UL,
        10UL,
        100UL,
        1_000UL,
        10_000UL,
        100_000UL,
        1_000_000UL,
        10_000_000UL,
        100_000_000UL,
        1_000_000_000UL,
        10_000_000_000UL,
        100_000_000_000UL,
        1_000_000_000_000UL,
        10_000_000_000_000UL,
        100_000_000_000_000UL,
        1_000_000_000_000_000UL,
        10_000_000_000_000_000UL,
        100_000_000_000_000_000UL,
        1_000_000_000_000_000_000UL,
        10_000_000_000_000_000_000UL
    };

    /// <summary>
    /// Tries to obtain 10^<paramref name="decimalPlaces"/> as a ulong for 0..19.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetPow10U64(byte decimalPlaces, out ulong pow10)
    {
        if (decimalPlaces <= 19)
        {
            pow10 = Pow10U64[decimalPlaces];
            return true;
        }

        pow10 = 0;
        return false;
    }
}
