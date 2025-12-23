using EccentricWare.Web3.DataTypes.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_Base58Tests
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    [TestMethod]
    public void TryDecodeBase58To32_RoundTripsCanonical()
    {
        byte[] bytes32 = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();
        string base58 = Base58Encode(bytes32);

        Span<byte> dst = stackalloc byte[32];
        Assert.IsTrue(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes(base58), dst));
        CollectionAssert.AreEqual(bytes32, dst.ToArray());
    }

    [TestMethod]
    public void TryDecodeBase58To32_AllowsNonCanonicalLeadingOnes()
    {
        // 32-byte value with 1 leading zero byte (canonical base58 starts with exactly 1 '1')
        byte[] bytes32 = new byte[32];
        bytes32[31] = 0x01; // small integer => many leading zero bytes; canonical has many '1's
        string base58 = Base58Encode(bytes32);

        // Prepend extra '1' => non-canonical, but TryDecodeBase58To32 does NOT enforce canonical.
        string nonCanonical = "1" + base58;

        Span<byte> dst = stackalloc byte[32];
        Assert.IsTrue(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes(nonCanonical), dst));
        CollectionAssert.AreEqual(bytes32, dst.ToArray());
    }

    [TestMethod]
    public void TryDecodeBase58To32_InvalidCharactersRejected()
    {
        Span<byte> dst = stackalloc byte[32];
        Assert.IsFalse(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes("0"), dst)); // '0' not in alphabet
        Assert.IsFalse(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes("O"), dst)); // 'O' not in alphabet
        Assert.IsFalse(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes("I"), dst)); // 'I' not in alphabet
        Assert.IsFalse(ByteUtils.TryDecodeBase58To32(Encoding.ASCII.GetBytes("l"), dst)); // 'l' not in alphabet
    }

    [TestMethod]
    public void TryDecodeBase58To64_RoundTripsCanonical_AndRejectsNonCanonical()
    {
        byte[] bytes64 = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        string base58 = Base58Encode(bytes64);

        Span<byte> dst = stackalloc byte[64];
        Assert.IsTrue(ByteUtils.TryDecodeBase58To64(Encoding.ASCII.GetBytes(base58), dst));
        CollectionAssert.AreEqual(bytes64, dst.ToArray());

        // Non-canonical by extra leading '1'
        Assert.IsFalse(ByteUtils.TryDecodeBase58To64(Encoding.ASCII.GetBytes("1" + base58), dst));
    }

    [TestMethod]
    public void TryDecodeBase58To64Chars_RoundTripsCanonical_AndRejectsNonCanonical()
    {
        byte[] bytes64 = Enumerable.Range(0, 64).Select(i => (byte)(255 - i)).ToArray();
        string base58 = Base58Encode(bytes64);

        Span<byte> dst = stackalloc byte[64];
        Assert.IsTrue(ByteUtils.TryDecodeBase58To64Chars(base58.AsSpan(), dst));
        CollectionAssert.AreEqual(bytes64, dst.ToArray());

        Assert.IsFalse(ByteUtils.TryDecodeBase58To64Chars(("1" + base58).AsSpan(), dst));
    }

    [TestMethod]
    public void TryDecodeBase58ToUInt256BigEndian_RoundTripsWithEncode_AndRejectsNonCanonical()
    {
        // Construct a deterministic 32-byte big-endian value.
        byte[] be = Enumerable.Range(0, 32).Select(i => (byte)(i + 10)).ToArray();

        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(be.AsSpan(0, 8));
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(be.AsSpan(8, 8));
        ulong u2 = BinaryPrimitives.ReadUInt64BigEndian(be.AsSpan(16, 8));
        ulong u3 = BinaryPrimitives.ReadUInt64BigEndian(be.AsSpan(24, 8));

        Span<byte> outUtf8 = stackalloc byte[128];
        int written = ByteUtils.EncodeBase58UInt256BigEndianToUtf8(u0, u1, u2, u3, outUtf8);
        Assert.IsGreaterThan(0, written);

        string encoded = Encoding.ASCII.GetString(outUtf8.Slice(0, written));

        // Should match reference Base58 encoding for these 32 bytes.
        Assert.AreEqual(Base58Encode(be), encoded);

        Assert.IsTrue(ByteUtils.TryDecodeBase58ToUInt256BigEndian(Encoding.ASCII.GetBytes(encoded),
            out ulong r0, out ulong r1, out ulong r2, out ulong r3));

        Assert.AreEqual(u0, r0);
        Assert.AreEqual(u1, r1);
        Assert.AreEqual(u2, r2);
        Assert.AreEqual(u3, r3);

        // Non-canonical: extra leading '1' should be rejected by canonical enforcement.
        Assert.IsFalse(ByteUtils.TryDecodeBase58ToUInt256BigEndian(Encoding.ASCII.GetBytes("1" + encoded),
            out _, out _, out _, out _));
    }

    [TestMethod]
    public void EncodeBase58UInt256BigEndian_Zero_Is32Ones_AndDestinationTooSmall()
    {
        Span<byte> dstTooSmall = stackalloc byte[31];
        int r = ByteUtils.EncodeBase58UInt256BigEndianToUtf8(0, 0, 0, 0, dstTooSmall);
        Assert.AreEqual(-1, r);

        Span<byte> dst = stackalloc byte[64];
        r = ByteUtils.EncodeBase58UInt256BigEndianToUtf8(0, 0, 0, 0, dst);
        Assert.AreEqual(32, r);

        string s = Encoding.ASCII.GetString(dst.Slice(0, r));
        Assert.AreEqual(new string('1', 32), s);
    }

    [TestMethod]
    public void CountLeadingZeroBytes256BigEndian_Behaviour()
    {
        Assert.AreEqual(32, ByteUtils.CountLeadingZeroBytes256BigEndian(0, 0, 0, 0));
        Assert.AreEqual(0, ByteUtils.CountLeadingZeroBytes256BigEndian(0x01UL << 63, 0, 0, 0)); // top bit set => no leading zero bytes
        Assert.AreEqual(8, ByteUtils.CountLeadingZeroBytes256BigEndian(0, 1UL << 56, 0, 0));             // first limb zero => +8 then leading zeros in second limb
        Assert.AreEqual(24, ByteUtils.CountLeadingZeroBytes256BigEndian(0, 0, 0, 1UL << 56));            // first 3 limbs zero => 24 + ...
    }

    private static string Base58Encode(ReadOnlySpan<byte> bytes)
    {
        int leadingZeros = 0;
        while (leadingZeros < bytes.Length && bytes[leadingZeros] == 0)
            leadingZeros++;

        BigInteger value = leadingZeros == bytes.Length
            ? BigInteger.Zero
            : new BigInteger(bytes.Slice(leadingZeros).ToArray(), isUnsigned: true, isBigEndian: true);

        // Digits are built in reverse
        Span<char> tmp = stackalloc char[128];
        int tmpLen = 0;

        while (value > 0)
        {
            value = BigInteger.DivRem(value, 58, out BigInteger rem);
            tmp[tmpLen++] = Alphabet[(int)rem];
        }

        // Result: leading '1's + reversed digits
        var sb = new StringBuilder(leadingZeros + tmpLen);
        sb.Append('1', leadingZeros);

        for (int i = tmpLen - 1; i >= 0; i--)
            sb.Append(tmp[i]);

        // For all-zero input, we want exactly N leading ones (already done).
        return sb.ToString();
    }
}
