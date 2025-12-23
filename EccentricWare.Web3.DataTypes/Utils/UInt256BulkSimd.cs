using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace EccentricWare.Web3.DataTypes.Utils;

/// <summary>
/// SIMD-accelerated bulk scanning utilities for <see cref="uint256"/> arrays.
/// Designed for rule-pack scans where you repeatedly search large, contiguous key arrays.
/// Falls back to scalar loops when SIMD is unavailable.
/// </summary>
public static class UInt256BulkSimd
{
    /// <summary>
    /// Finds the first index of <paramref name="needle"/> in <paramref name="haystack"/>, or -1 if not found.
    /// Uses AVX2 when supported.
    /// </summary>
    /// <param name="haystack">The values to scan.</param>
    /// <param name="needle">The value to match.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        if (haystack.IsEmpty)
            return -1;

        if (Avx2.IsSupported)
            return IndexOfAvx2(haystack, in needle);

        return IndexOfScalar(haystack, in needle);
    }

    /// <summary>
    /// Counts occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.
    /// Uses AVX2 when supported.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountEquals(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        if (haystack.IsEmpty)
            return 0;

        if (Avx2.IsSupported)
            return CountEqualsAvx2(haystack, in needle);

        return CountEqualsScalar(haystack, in needle);
    }

    /// <summary>
    /// Writes matching indices for <paramref name="needle"/> into <paramref name="destinationIndices"/> and returns how many were written.
    /// Scans the full span; stops writing when the destination is full.
    /// Uses AVX2 when supported.
    /// </summary>
    /// <param name="haystack">The values to scan.</param>
    /// <param name="needle">The value to match.</param>
    /// <param name="destinationIndices">The destination for indices of matches.</param>
    /// <returns>The number of indices written to <paramref name="destinationIndices"/>.</returns>
    public static int FindAllEquals(ReadOnlySpan<uint256> haystack, in uint256 needle, Span<int> destinationIndices)
    {
        if (haystack.IsEmpty || destinationIndices.IsEmpty)
            return 0;

        if (Avx2.IsSupported)
            return FindAllEqualsAvx2(haystack, in needle, destinationIndices);

        return FindAllEqualsScalar(haystack, in needle, destinationIndices);
    }

    /// <summary>
    /// Returns true if <paramref name="haystack"/> contains <paramref name="needle"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains(ReadOnlySpan<uint256> haystack, in uint256 needle)
        => IndexOf(haystack, in needle) >= 0;

    #region AVX2 Implementations

    /// <summary>
    /// AVX2 implementation of IndexOf for uint256 values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfAvx2(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        Vector256<ulong> needleVec = ReadUInt256AsVector(in needle);

        ref EccentricWare.Web3.DataTypes.uint256 start = ref MemoryMarshal.GetReference(haystack);
        int length = haystack.Length;

        int i = 0;

        // Unroll by 2 to reduce loop overhead.
        int lastUnrolled = length - 2;
        for (; i <= lastUnrolled; i += 2)
        {
            if (EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i)), needleVec))
                return i;

            if (EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i + 1)), needleVec))
                return i + 1;
        }

        for (; i < length; i++)
        {
            if (EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i)), needleVec))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// AVX2 implementation of CountEquals for uint256 values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountEqualsAvx2(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        Vector256<ulong> needleVec = ReadUInt256AsVector(in needle);

        ref uint256 start = ref MemoryMarshal.GetReference(haystack);
        int length = haystack.Length;

        int count = 0;
        int i = 0;

        int lastUnrolled = length - 4;
        for (; i <= lastUnrolled; i += 4)
        {
            count += EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i + 0)), needleVec) ? 1 : 0;
            count += EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i + 1)), needleVec) ? 1 : 0;
            count += EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i + 2)), needleVec) ? 1 : 0;
            count += EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i + 3)), needleVec) ? 1 : 0;
        }

        for (; i < length; i++)
            count += EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i)), needleVec) ? 1 : 0;

        return count;
    }

    /// <summary>
    /// AVX2 implementation of FindAllEquals for uint256 values.
    /// </summary>
    private static int FindAllEqualsAvx2(ReadOnlySpan<uint256> haystack, in uint256 needle, Span<int> destinationIndices)
    {
        Vector256<ulong> needleVec = ReadUInt256AsVector(in needle);

        ref uint256 start = ref MemoryMarshal.GetReference(haystack);
        int length = haystack.Length;

        int written = 0;

        for (int i = 0; i < length; i++)
        {
            if (EqualsAvx2(ReadUInt256AsVectorWithRef(ref Unsafe.Add(ref start, i)), needleVec))
            {
                destinationIndices[written++] = i;
                if (written == destinationIndices.Length)
                    break;
            }
        }

        return written;
    }

    /// <summary>
    /// Loads a uint256 value as a <see cref="Vector256{UInt64}"/> using an unaligned read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> ReadUInt256AsVector(in uint256 value)
    {
        ref byte src = ref Unsafe.As<uint256, byte>(ref Unsafe.AsRef(in value));
        return Unsafe.ReadUnaligned<Vector256<ulong>>(ref src);
    }

    /// <summary>
    /// Loads a uint256 value as a <see cref="Vector256{UInt64}"/> using an unaligned read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> ReadUInt256AsVectorWithRef(ref uint256 value)
    {
        ref byte src = ref Unsafe.As<uint256, byte>(ref value);
        return Unsafe.ReadUnaligned<Vector256<ulong>>(ref src);
    }

    /// <summary>
    /// Returns true if two <see cref="Vector256{UInt64}"/> values are equal across all 32 bytes using AVX2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsAvx2(Vector256<ulong> left, Vector256<ulong> right)
    {
        Vector256<ulong> cmp = Avx2.CompareEqual(left, right);

        // If all bytes are 0xFF then MoveMask returns 0xFFFFFFFF (-1).
        return Avx2.MoveMask(cmp.AsByte()) == -1;
    }

    #endregion

    #region Scalar Implementations

    /// <summary>
    /// Scalar implementation of IndexOf for uint256 values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfScalar(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        for (int i = 0; i < haystack.Length; i++)
        {
            if (haystack[i].Equals(needle))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Scalar implementation of CountEquals for uint256 values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountEqualsScalar(ReadOnlySpan<uint256> haystack, in uint256 needle)
    {
        int count = 0;
        for (int i = 0; i < haystack.Length; i++)
            count += haystack[i].Equals(needle) ? 1 : 0;

        return count;
    }

    /// <summary>
    /// Scalar implementation of FindAllEquals for uint256 values.
    /// </summary>
    private static int FindAllEqualsScalar(ReadOnlySpan<uint256> haystack, in uint256 needle, Span<int> destinationIndices)
    {
        int written = 0;
        for (int i = 0; i < haystack.Length; i++)
        {
            if (haystack[i].Equals(needle))
            {
                destinationIndices[written++] = i;
                if (written == destinationIndices.Length)
                    break;
            }
        }
        return written;
    }

    #endregion
}