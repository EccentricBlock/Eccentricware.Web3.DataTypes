
namespace EccentricWare.Web3.DataTypes.Comparers;

/// <summary>
/// High-performance comparer for <see cref="int256"/> suitable for:
/// - Dictionary/HashSet keys (IEqualityComparer)
/// - Sorted collections (IComparer)
/// Uses the struct's optimised equality and comparison implementations.
/// </summary>
public sealed class Int256Comparer : IEqualityComparer<int256>, IComparer<int256>
{
    /// <summary>
    /// Singleton instance to avoid allocations in hot paths.
    /// </summary>
    public static readonly Int256Comparer Instance = new Int256Comparer();

    private Int256Comparer() { }

    /// <summary>
    /// Returns whether two values are equal.
    /// </summary>
    public bool Equals(int256 x, int256 y) => x.Equals(y);

    /// <summary>
    /// Returns a hash code for the specified value.
    /// </summary>
    public int GetHashCode(int256 obj) => obj.GetHashCode();

    /// <summary>
    /// Compares two values numerically (signed, 256-bit).
    /// </summary>
    public int Compare(int256 x, int256 y) => x.CompareTo(y);

    /// <summary>
    /// Hot-path helper that avoids copying operands at call sites that can use <c>in</c>.
    /// This does not satisfy interface contracts but is useful inside tight loops.
    /// </summary>
    public static bool Equals(in int256 x, in int256 y) => x.Equals(y);

    /// <summary>
    /// Hot-path helper that avoids copying operands at call sites that can use <c>in</c>.
    /// This does not satisfy interface contracts but is useful inside tight loops.
    /// </summary>
    public static int Compare(in int256 x, in int256 y) => x.CompareTo(y);
}
