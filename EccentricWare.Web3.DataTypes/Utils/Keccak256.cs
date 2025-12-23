using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Utils;

/// <summary>
/// Minimal Keccak-256 implementation (Ethereum Keccak, not NIST SHA3) intended for cold-path selector derivation.
/// </summary>
public static class Keccak256
{
    private const int RateBytes = 136; // 1088-bit rate for Keccak-256
    private const int OutputBytes = 32;

    private static readonly ulong[] RoundConstants =
    {
        0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808AUL, 0x8000000080008000UL,
        0x000000000000808BUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008AUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000AUL,
        0x000000008000808BUL, 0x800000000000008BUL, 0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800AUL, 0x800000008000000AUL,
        0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
    };

    private static readonly int[] RhoOffsets =
    {
        0,  1, 62, 28, 27,
        36, 44,  6, 55, 20,
        3, 10, 43, 25, 39,
        41, 45, 15, 21,  8,
        18,  2, 61, 56, 14
    };

    private static readonly int[] PiLane =
    {
        0,  6, 12, 18, 24,
        3,  9, 10, 16, 22,
        1,  7, 13, 19, 20,
        4,  5, 11, 17, 23,
        2,  8, 14, 15, 21
    };

    /// <summary>
    /// Computes Keccak-256(input) into a 32-byte destination buffer.
    /// </summary>
    /// <param name="input">Input bytes.</param>
    /// <param name="hash32">Destination span (must be 32 bytes).</param>
    public static void ComputeHash(ReadOnlySpan<byte> input, Span<byte> hash32)
    {
        if (hash32.Length != OutputBytes)
            ThrowHelper.ThrowArgumentExceptionFixedSize(nameof(hash32), OutputBytes);

        Span<ulong> state = stackalloc ulong[25];
        state.Clear();

        // Absorb full blocks.
        while (input.Length >= RateBytes)
        {
            AbsorbBlock(state, input.Slice(0, RateBytes));
            KeccakF1600(state);
            input = input.Slice(RateBytes);
        }

        // Absorb final block with Keccak padding (0x01 ... 0x80).
        Span<byte> block = stackalloc byte[RateBytes];
        block.Clear();
        input.CopyTo(block);

        block[input.Length] ^= 0x01;
        block[RateBytes - 1] ^= 0x80;

        AbsorbBlock(state, block);
        KeccakF1600(state);

        // Squeeze 32 bytes from the state.
        Span<byte> outBytes = stackalloc byte[RateBytes];
        Squeeze(state, outBytes);
        outBytes.Slice(0, OutputBytes).CopyTo(hash32);
    }

    /// <summary>
    /// XORs a 136-byte message block into the state (little-endian lanes).
    /// </summary>
    /// <param name="state">State lanes.</param>
    /// <param name="block">Block bytes (must be 136 bytes).</param>
    private static void AbsorbBlock(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        for (int i = 0; i < RateBytes / 8; i++)
        {
            ulong lane = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8, 8));
            state[i] ^= lane;
        }
    }

    /// <summary>
    /// Squeezes the rate portion of the state into a byte buffer (little-endian lanes).
    /// </summary>
    /// <param name="state">State lanes.</param>
    /// <param name="outputRateBytes">Output buffer (must be at least 136 bytes).</param>
    private static void Squeeze(ReadOnlySpan<ulong> state, Span<byte> outputRateBytes)
    {
        for (int i = 0; i < RateBytes / 8; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(outputRateBytes.Slice(i * 8, 8), state[i]);
    }

    /// <summary>
    /// Keccak-f[1600] permutation (24 rounds).
    /// </summary>
    /// <param name="a">State lanes.</param>
    private static void KeccakF1600(Span<ulong> a)
    {
        Span<ulong> b = stackalloc ulong[25];
        Span<ulong> c = stackalloc ulong[5];
        Span<ulong> d = stackalloc ulong[5];

        for (int round = 0; round < 24; round++)
        {
            // Theta
            for (int x = 0; x < 5; x++)
                c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];

            for (int x = 0; x < 5; x++)
                d[x] = c[(x + 4) % 5] ^ Rotl64(c[(x + 1) % 5], 1);

            for (int i = 0; i < 25; i++)
                a[i] ^= d[i % 5];

            // Rho + Pi
            for (int i = 0; i < 25; i++)
            {
                int j = PiLane[i];
                b[j] = Rotl64(a[i], RhoOffsets[i]);
            }

            // Chi
            for (int y = 0; y < 5; y++)
            {
                int row = y * 5;
                ulong b0 = b[row + 0];
                ulong b1 = b[row + 1];
                ulong b2 = b[row + 2];
                ulong b3 = b[row + 3];
                ulong b4 = b[row + 4];

                a[row + 0] = b0 ^ (~b1 & b2);
                a[row + 1] = b1 ^ (~b2 & b3);
                a[row + 2] = b2 ^ (~b3 & b4);
                a[row + 3] = b3 ^ (~b4 & b0);
                a[row + 4] = b4 ^ (~b0 & b1);
            }

            // Iota
            a[0] ^= RoundConstants[round];
        }
    }

    /// <summary>
    /// Rotates a 64-bit integer left.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rotl64(ulong x, int n)
        => (x << n) | (x >> (64 - n));
}