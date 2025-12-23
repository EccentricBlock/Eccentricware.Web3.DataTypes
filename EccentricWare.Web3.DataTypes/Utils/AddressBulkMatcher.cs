using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace EccentricWare.Web3.DataTypes.Utils;

/// <summary>
/// Bulk matching utilities for <see cref="Address"/>.
/// Provides both hash-based (compiled) membership and SIMD-assisted linear scans.
/// </summary>
public static class AddressBulkMatcher
{
    /// <summary>
    /// Returns true if <paramref name="needle"/> is contained in <paramref name="haystack"/>.
    /// Uses AVX2 for byte-wise equality if available, otherwise falls back to scalar limb comparison.
    /// Intended for small arrays (dozens to a few hundred).
    /// </summary>
    public static bool ContainsLinear(ReadOnlySpan<Address> haystack, Address needle)
    {
        if (haystack.IsEmpty)
            return false;

        if (Avx2.IsSupported)
            return ContainsLinearAvx2(haystack, needle);

        // Scalar fallback: compare u0..u3 and type.
        AddressKey.ReadKey(in needle, out ulong n0, out ulong n1, out ulong n2, out ulong n3, out byte nType);

        for (int i = 0; i < haystack.Length; i++)
        {
            ref readonly Address candidate = ref haystack[i];
            if ((byte)candidate.Type != nType)
                continue;

            AddressKey.ReadKey(in candidate, out ulong c0, out ulong c1, out ulong c2, out ulong c3, out _);

            if (c0 == n0 && c1 == n1 && c2 == n2 && c3 == n3)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Writes indices of probes that are contained in <paramref name="set"/> into <paramref name="matchedProbeIndices"/>.
    /// Returns the number of indices written.
    /// </summary>
    public static int Match(AddressSet set, ReadOnlySpan<Address> probes, Span<int> matchedProbeIndices)
    {
        if (matchedProbeIndices.Length == 0 || probes.IsEmpty)
            return 0;

        int written = 0;
        for (int i = 0; i < probes.Length; i++)
        {
            if (set.Contains(in probes[i]))
            {
                if (written == matchedProbeIndices.Length)
                    break;

                matchedProbeIndices[written++] = i;
            }
        }

        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsLinearAvx2(ReadOnlySpan<Address> haystack, Address needle)
    {
        AddressKey.ReadKeyBytes32(in needle, out Vector256<byte> needleBytes32, out byte needleType);

        for (int i = 0; i < haystack.Length; i++)
        {
            ref readonly Address candidate = ref haystack[i];
            if ((byte)candidate.Type != needleType)
                continue;

            AddressKey.ReadKeyBytes32(in candidate, out Vector256<byte> candidateBytes32, out _);

            // Compare all 32 bytes.
            Vector256<byte> eq = Avx2.CompareEqual(candidateBytes32, needleBytes32);
            if (Avx2.MoveMask(eq) == -1)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Internal key reader that relies on <see cref="Address"/> storing its 32-byte backing value in the first 32 bytes of the struct.
    /// This matches the layout in your implementation (four ulongs first).
    /// </summary>
    private static class AddressKey
    {
        /// <summary>
        /// Reads the key as four 64-bit limbs plus the address type byte.
        /// Limb endianness is the in-memory representation of the struct (not big-endian wire format).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadKey(in Address address, out ulong u0, out ulong u1, out ulong u2, out ulong u3, out byte addressType)
        {
            // We assume Address is unmanaged and starts with four ulong fields.
            // This is a deliberate performance trade-off for hot-path matching.
            ref byte b = ref Unsafe.As<Address, byte>(ref Unsafe.AsRef(in address));

            u0 = Unsafe.ReadUnaligned<ulong>(ref b);
            u1 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8));
            u2 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 16));
            u3 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 24));

            addressType = (byte)address.Type;
        }

        /// <summary>
        /// Reads the first 32 bytes of the key as a <see cref="Vector256{Byte}"/> (AVX2 path), plus the address type byte.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadKeyBytes32(in Address address, out Vector256<byte> keyBytes32, out byte addressType)
        {
            ref byte b = ref Unsafe.As<Address, byte>(ref Unsafe.AsRef(in address));
            keyBytes32 = Unsafe.ReadUnaligned<Vector256<byte>>(ref b);
            addressType = (byte)address.Type;
        }
    }
}

/// <summary>
/// A compiled, allocation-stable hash set for <see cref="Address"/> membership checks.
/// Optimised for large rule packs (thousands to millions) with predictable performance.
/// </summary>
public sealed class AddressSet
{
    private readonly ulong[] _hashes; // 0 = empty; storedHash is always non-zero
    private readonly ulong[] _u0;
    private readonly ulong[] _u1;
    private readonly ulong[] _u2;
    private readonly ulong[] _u3;
    private readonly byte[] _types;
    private readonly int _mask;
    private readonly int _count;

    /// <summary>Returns the number of elements inserted into the set.</summary>
    public int Count => _count;

    private AddressSet(ulong[] hashes, ulong[] u0, ulong[] u1, ulong[] u2, ulong[] u3, byte[] types, int count)
    {
        _hashes = hashes;
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
        _types = types;
        _mask = hashes.Length - 1;
        _count = count;
    }

    /// <summary>
    /// Builds a compiled <see cref="AddressSet"/> from the provided <paramref name="addresses"/>.
    /// </summary>
    /// <param name="addresses">Addresses to insert.</param>
    /// <param name="loadFactor">Hash table load factor (0.50..0.90). Lower is faster but uses more memory.</param>
    public static AddressSet Build(ReadOnlySpan<Address> addresses, double loadFactor = 0.72)
    {
        if (addresses.IsEmpty)
            return new AddressSet(Array.Empty<ulong>(), Array.Empty<ulong>(), Array.Empty<ulong>(), Array.Empty<ulong>(), Array.Empty<ulong>(), Array.Empty<byte>(), 0);

        if (loadFactor < 0.50 || loadFactor > 0.90)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), "Load factor must be between 0.50 and 0.90.");

        int desiredCapacity = (int)Math.Ceiling(addresses.Length / loadFactor);
        int capacity = NextPowerOfTwo(Math.Max(4, desiredCapacity));

        var hashes = new ulong[capacity];
        var u0 = new ulong[capacity];
        var u1 = new ulong[capacity];
        var u2 = new ulong[capacity];
        var u3 = new ulong[capacity];
        var types = new byte[capacity];

        int inserted = 0;

        for (int i = 0; i < addresses.Length; i++)
        {
            ref readonly Address address = ref addresses[i];
            ReadKey(in address, out ulong a0, out ulong a1, out ulong a2, out ulong a3, out byte t);

            ulong h = Hash(a0, a1, a2, a3, t) | 1UL; // ensure non-zero
            int slot = (int)h & (capacity - 1);

            // Linear probing.
            while (hashes[slot] != 0UL)
            {
                // Optional de-dup: if identical, skip.
                if (hashes[slot] == h && types[slot] == t && u0[slot] == a0 && u1[slot] == a1 && u2[slot] == a2 && u3[slot] == a3)
                    goto Next;

                slot = (slot + 1) & (capacity - 1);
            }

            hashes[slot] = h;
            u0[slot] = a0;
            u1[slot] = a1;
            u2[slot] = a2;
            u3[slot] = a3;
            types[slot] = t;
            inserted++;

        Next:
            continue;
        }

        return new AddressSet(hashes, u0, u1, u2, u3, types, inserted);
    }

    /// <summary>
    /// Returns true if the set contains <paramref name="address"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Address address)
    {
        if (_hashes.Length == 0)
            return false;

        ReadKey(in address, out ulong a0, out ulong a1, out ulong a2, out ulong a3, out byte t);

        ulong h = Hash(a0, a1, a2, a3, t) | 1UL;
        int slot = (int)h & _mask;

        while (true)
        {
            ulong stored = _hashes[slot];
            if (stored == 0UL)
                return false;

            if (stored == h &&
                _types[slot] == t &&
                _u0[slot] == a0 &&
                _u1[slot] == a1 &&
                _u2[slot] == a2 &&
                _u3[slot] == a3)
            {
                return true;
            }

            slot = (slot + 1) & _mask;
        }
    }

    /// <summary>
    /// Computes a stable 64-bit hash for the provided 32-byte key and type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Hash(ulong u0, ulong u1, ulong u2, ulong u3, byte type)
    {
        // MurmurHash3 finaliser-style mixing; fast and stable.
        ulong x =
            u0 ^
            BitOperations.RotateLeft(u1, 13) ^
            BitOperations.RotateLeft(u2, 27) ^
            BitOperations.RotateLeft(u3, 41) ^
            ((ulong)type * 0x9E3779B97F4A7C15UL);

        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCDUL;
        x ^= x >> 33;
        x *= 0xC4CEB9FE1A85EC53UL;
        x ^= x >> 33;
        return x;
    }

    /// <summary>
    /// Reads the 32-byte backing key from <paramref name="address"/> plus its type byte.
    /// This relies on Address storing its four ulong fields first (your current layout).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadKey(in Address address, out ulong u0, out ulong u1, out ulong u2, out ulong u3, out byte addressType)
    {
        ref byte b = ref Unsafe.As<Address, byte>(ref Unsafe.AsRef(in address));

        u0 = Unsafe.ReadUnaligned<ulong>(ref b);
        u1 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8));
        u2 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 16));
        u3 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 24));

        addressType = (byte)address.Type;
    }

    /// <summary>Returns the next power-of-two greater than or equal to <paramref name="value"/>.</summary>
    private static int NextPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}