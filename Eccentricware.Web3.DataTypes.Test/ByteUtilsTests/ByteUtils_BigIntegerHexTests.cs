using EccentricWare.Web3.DataTypes.Utils;
using System.Numerics;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_BigIntegerHexTests
{
    [TestMethod]
    public void TryParseBigIntegerHexUnsignedUtf8_EmptyIsZero()
    {
        Assert.IsTrue(ByteUtils.TryParseBigIntegerHexUnsignedUtf8(ReadOnlySpan<byte>.Empty, maxHexDigits: 64, out BigInteger value));
        Assert.AreEqual(BigInteger.Zero, value);
    }

    [TestMethod]
    public void TryParseBigIntegerHexUnsignedUtf8_OddAndEvenLengths()
    {
        Assert.IsTrue(ByteUtils.TryParseBigIntegerHexUnsignedUtf8("f"u8, maxHexDigits: 64, out BigInteger v));
        Assert.AreEqual(new BigInteger(15), v);

        Assert.IsTrue(ByteUtils.TryParseBigIntegerHexUnsignedUtf8("0f"u8, maxHexDigits: 64, out v));
        Assert.AreEqual(new BigInteger(15), v);

        Assert.IsTrue(ByteUtils.TryParseBigIntegerHexUnsignedUtf8("010203"u8, maxHexDigits: 64, out v));
        Assert.AreEqual(new BigInteger(new byte[] { 0x01, 0x02, 0x03 }, isUnsigned: true, isBigEndian: true), v);
    }

    [TestMethod]
    public void TryParseBigIntegerHexUnsignedUtf8_RespectsMaxDigits()
    {
        ReadOnlySpan<byte> hex = Encoding.ASCII.GetBytes(new string('a', 33));
        Assert.IsFalse(ByteUtils.TryParseBigIntegerHexUnsignedUtf8(hex, maxHexDigits: 32, out _));
    }

    [TestMethod]
    public void TryParseBigIntegerHexUnsignedUtf8_InvalidHexRejected()
    {
        Assert.IsFalse(ByteUtils.TryParseBigIntegerHexUnsignedUtf8("0g"u8, maxHexDigits: 64, out _));
    }

    [TestMethod]
    public void TryWriteHexMinimalUtf8_FormatsAsExpected()
    {
        Span<byte> dst = stackalloc byte[128];

        // Zero => "0" / "0x0"
        byte[] zero32 = new byte[32];
        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(zero32, write0xPrefix: false, uppercase: false, dst, out int written));
        Assert.AreEqual("0", Encoding.ASCII.GetString(dst.Slice(0, written)));

        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(zero32, write0xPrefix: true, uppercase: false, dst, out written));
        Assert.AreEqual("0x0", Encoding.ASCII.GetString(dst.Slice(0, written)));

        // Leading zero byte + odd digit (0x0f => "f")
        byte[] mag = { 0x00, 0x0F };
        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(mag, write0xPrefix: false, uppercase: false, dst, out written));
        Assert.AreEqual("f", Encoding.ASCII.GetString(dst.Slice(0, written)));

        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(mag, write0xPrefix: true, uppercase: true, dst, out written));
        Assert.AreEqual("0xF", Encoding.ASCII.GetString(dst.Slice(0, written)));

        // 0x0123 => "123"
        byte[] mag2 = { 0x01, 0x23 };
        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(mag2, write0xPrefix: false, uppercase: false, dst, out written));
        Assert.AreEqual("123", Encoding.ASCII.GetString(dst.Slice(0, written)));

        // Exact byte (0xab => "ab")
        byte[] mag3 = { 0xAB };
        Assert.IsTrue(ByteUtils.TryWriteHexMinimalUtf8(mag3, write0xPrefix: false, uppercase: false, dst, out written));
        Assert.AreEqual("ab", Encoding.ASCII.GetString(dst.Slice(0, written)));
    }

    [TestMethod]
    public void TryWriteHexMinimalUtf8_DestinationTooSmall_ReturnsFalse()
    {
        byte[] mag = { 0x01, 0x23 }; // "123" needs 3 bytes
        Span<byte> dst = stackalloc byte[2];
        Assert.IsFalse(ByteUtils.TryWriteHexMinimalUtf8(mag, write0xPrefix: false, uppercase: false, dst, out _));
    }
}