using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Comparers;

/// <summary>
/// High-performance comparer for <see cref="uint256"/> suitable for:
/// - Dictionary/HashSet keys (IEqualityComparer)
/// - Sorted collections (IComparer)
/// Uses the struct's optimised equality and hashing implementations.
/// </summary>
public sealed class UInt256Comparer : IEqualityComparer<uint256>, IComparer<uint256>
{
    /// <summary>
    /// Singleton instance to avoid allocations in hot paths.
    /// </summary>
    public static readonly UInt256Comparer Instance = new UInt256Comparer();

    private UInt256Comparer() { }

    /// <summary>
    /// Returns whether two values are equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(uint256 x, uint256 y) => x.Equals(y);

    /// <summary>
    /// Returns a hash code for the specified value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(uint256 obj) => obj.GetHashCode();

    /// <summary>
    /// Compares two values numerically (unsigned, 256-bit).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(uint256 x, uint256 y) => x.CompareTo(y);

    /// <summary>
    /// Hot-path helper that avoids copying operands at call sites that can use <c>in</c>.
    /// This does not satisfy interface contracts but is useful inside tight loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(in uint256 x, in uint256 y) => x.Equals(y);

    /// <summary>
    /// Hot-path helper that avoids copying operands at call sites that can use <c>in</c>.
    /// This does not satisfy interface contracts but is useful inside tight loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(in uint256 x, in uint256 y) => x.CompareTo(y);
}