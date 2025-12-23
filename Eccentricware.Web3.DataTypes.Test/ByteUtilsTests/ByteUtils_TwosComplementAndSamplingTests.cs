using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.Intrinsics;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_TwosComplementAndSamplingTests
{
    [TestMethod]
    public void NegateTwosComplement256_MatchesMod2Pow256()
    {
        // A few fixed values that exercise carries.
        AssertNegationMatches(0UL, 0UL, 0UL, 0UL);
        AssertNegationMatches(1UL, 0UL, 0UL, 0UL);
        AssertNegationMatches(0UL, 1UL, 0UL, 0UL);
        AssertNegationMatches(ulong.MaxValue, 0UL, 0UL, 0UL);
        AssertNegationMatches(0UL, 0UL, 0UL, 1UL);
        AssertNegationMatches(0x0123456789ABCDEFUL, 0x0UL, 0x1111111111111111UL, 0x2222222222222222UL);
    }

    [TestMethod]
    public void SampleUInt64LittleEndian_Behaviour()
    {
        byte[] bytes = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // Full 8 bytes from index 0: little-endian
        ulong v0 = ByteUtils.SampleUInt64LittleEndian(bytes, 0);
        Assert.AreEqual(0x0807060504030201UL, v0);

        // Partial from index 4: bytes[4..] = 5,6,7,8,9 => 0x0908070605
        ulong v4 = ByteUtils.SampleUInt64LittleEndian(bytes, 4);
        Assert.AreEqual(0x0000000908070605UL, v4);

        // Out of range => 0
        Assert.AreEqual(0UL, ByteUtils.SampleUInt64LittleEndian(bytes, 99));
    }

    [TestMethod]
    public void AsReadOnlyBytes_ExposesRawBytes()
    {
        ulong value = 0x1122334455667788UL;
        var span = ByteUtils.AsReadOnlyBytes(in value);

        CollectionAssert.AreEqual(BitConverter.GetBytes(value), span.ToArray());
    }

    [TestMethod]
    public void ReadVector256_ReadsAllBytes()
    {
        byte[] bytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Vector256<byte> v = ByteUtils.ReadVector256(bytes);

        for (int i = 0; i < 32; i++)
            Assert.AreEqual((byte)i, v.GetElement(i));
    }

    private static void AssertNegationMatches(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        ByteUtils.NegateTwosComplement256(u0, u1, u2, u3, out ulong r0, out ulong r1, out ulong r2, out ulong r3);

        BigInteger x = FromLimbsLE(u0, u1, u2, u3);
        BigInteger mod = BigInteger.One << 256;
        BigInteger expected = (mod - (x % mod)) % mod;

        ulong[] expectedLE = ToLimbsLE(expected);

        Assert.AreEqual(expectedLE[0], r0);
        Assert.AreEqual(expectedLE[1], r1);
        Assert.AreEqual(expectedLE[2], r2);
        Assert.AreEqual(expectedLE[3], r3);
    }

    private static BigInteger FromLimbsLE(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        byte[] le = new byte[32];
        BinaryPrimitives.WriteUInt64LittleEndian(le.AsSpan(0, 8), u0);
        BinaryPrimitives.WriteUInt64LittleEndian(le.AsSpan(8, 8), u1);
        BinaryPrimitives.WriteUInt64LittleEndian(le.AsSpan(16, 8), u2);
        BinaryPrimitives.WriteUInt64LittleEndian(le.AsSpan(24, 8), u3);
        return new BigInteger(le, isUnsigned: true, isBigEndian: false);
    }

    private static ulong[] ToLimbsLE(BigInteger value)
    {
        byte[] le = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Resize(ref le, 32);

        return new[]
        {
                BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(0, 8)),
                BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(8, 8)),
                BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(16, 8)),
                BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(24, 8)),
            };
    }
}