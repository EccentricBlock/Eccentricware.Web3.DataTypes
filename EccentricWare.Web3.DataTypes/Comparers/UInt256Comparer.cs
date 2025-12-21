using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Comparers;

/// <summary>
/// High-throughput equality comparer for <see cref="uint256"/> suitable for hash-based indexes.
/// </summary>

public sealed class UInt256Comparer : IEqualityComparer<uint256>
{
    /// <summary>Singleton instance.</summary>
    public static readonly UInt256Comparer Instance = new();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(uint256 leftKey, uint256 rightKey) => leftKey.Equals(rightKey);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(uint256 key)
    {
        ulong h = key.GetStableHash64();
        return (int)(h ^ (h >> 32));
    }
}