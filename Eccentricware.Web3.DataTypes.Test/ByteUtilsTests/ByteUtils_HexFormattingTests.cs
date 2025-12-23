using EccentricWare.Web3.DataTypes.Utils;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_HexFormattingTests
{
    [TestMethod]
    public void WriteHexUInt32CharsFixed8_WritesLowerAndUpper()
    {
        Span<char> dst = stackalloc char[8];

        ByteUtils.WriteHexUInt32CharsFixed8(dst, 0xDEADBEEFu, uppercase: false);
        Assert.AreEqual("deadbeef", dst.ToString());

        ByteUtils.WriteHexUInt32CharsFixed8(dst, 0xDEADBEEFu, uppercase: true);
        Assert.AreEqual("DEADBEEF", dst.ToString());
    }

    [TestMethod]
    public void WriteHexUInt32Utf8Fixed8_WritesLowerAndUpper()
    {
        Span<byte> dst = stackalloc byte[8];

        ByteUtils.WriteHexUInt32Utf8Fixed8(dst, 0xDEADBEEFu, uppercase: false);
        Assert.AreEqual("deadbeef", Encoding.ASCII.GetString(dst));

        ByteUtils.WriteHexUInt32Utf8Fixed8(dst, 0xDEADBEEFu, uppercase: true);
        Assert.AreEqual("DEADBEEF", Encoding.ASCII.GetString(dst));
    }

    [TestMethod]
    public void WriteHexUInt64CharsFixed16_WritesLowerAndUpper()
    {
        Span<char> dst = stackalloc char[16];

        ByteUtils.WriteHexUInt64CharsFixed16(dst, 0x0123456789ABCDEFUL, uppercase: false);
        Assert.AreEqual("0123456789abcdef", dst.ToString());

        ByteUtils.WriteHexUInt64CharsFixed16(dst, 0x0123456789ABCDEFUL, uppercase: true);
        Assert.AreEqual("0123456789ABCDEF", dst.ToString());
    }

    [TestMethod]
    public void WriteHexUInt64Utf8Fixed16_WritesUsingAlphabet()
    {
        Span<byte> dst = stackalloc byte[16];

        ByteUtils.WriteHexUInt64Utf8Fixed16(dst, 0x0123456789ABCDEFUL, ByteUtils.HexBytesLower);
        Assert.AreEqual("0123456789abcdef", Encoding.ASCII.GetString(dst));

        ByteUtils.WriteHexUInt64Utf8Fixed16(dst, 0x0123456789ABCDEFUL, ByteUtils.HexBytesUpper);
        Assert.AreEqual("0123456789ABCDEF", Encoding.ASCII.GetString(dst));
    }

    [TestMethod]
    public void WriteHexUInt64_Method_WritesCorrectly()
    {
        Span<char> dst = stackalloc char[16];
        ByteUtils.WriteHexUInt64(dst, 0x0123456789ABCDEFUL, uppercase: false);
        Assert.AreEqual("0123456789abcdef", dst.ToString());
    }

    [TestMethod]
    public void WriteHexUInt64Utf8_Method_WritesCorrectly()
    {
        Span<byte> dst = stackalloc byte[16];
        ByteUtils.WriteHexUInt64Utf8(dst, 0x0123456789ABCDEFUL, ByteUtils.HexBytesLower);
        Assert.AreEqual("0123456789abcdef", Encoding.ASCII.GetString(dst));
    }

    [TestMethod]
    public void TryEncodeHexChars_And_TryEncodeHexUtf8_Work()
    {
        byte[] src = { 0x00, 0x01, 0xAB, 0xFF };

        Span<char> dstChars = stackalloc char[8];
        Assert.IsTrue(ByteUtils.TryEncodeHexChars(src, dstChars, uppercase: false));
        Assert.AreEqual("0001abff", dstChars.ToString());

        Span<byte> dstUtf8 = stackalloc byte[8];
        Assert.IsTrue(ByteUtils.TryEncodeHexUtf8(src, dstUtf8, uppercase: true));
        Assert.AreEqual("0001ABFF", Encoding.ASCII.GetString(dstUtf8));

        // Destination too small
        Span<char> tooSmallChars = stackalloc char[7];
        Assert.IsFalse(ByteUtils.TryEncodeHexChars(src, tooSmallChars, uppercase: false));

        Span<byte> tooSmallUtf8 = stackalloc byte[7];
        Assert.IsFalse(ByteUtils.TryEncodeHexUtf8(src, tooSmallUtf8, uppercase: false));
    }

    [TestMethod]
    public void GetMaxHexUtf8LengthFromMagnitudeByteCount_WorstCase()
    {
        // magnitudeByteCount=0 still must reserve for at least "0"
        Assert.AreEqual(1, ByteUtils.GetMaxHexUtf8LengthFromMagnitudeByteCount(0, includeSign: false, include0xPrefix: false));
        Assert.AreEqual(1 + 2, ByteUtils.GetMaxHexUtf8LengthFromMagnitudeByteCount(0, includeSign: false, include0xPrefix: true));
        Assert.AreEqual(1 + 1 + 2, ByteUtils.GetMaxHexUtf8LengthFromMagnitudeByteCount(0, includeSign: true, include0xPrefix: true));

        Assert.AreEqual(2 * 32, ByteUtils.GetMaxHexUtf8LengthFromMagnitudeByteCount(32, includeSign: false, include0xPrefix: false));
        Assert.AreEqual(1 + 2 + (2 * 32), ByteUtils.GetMaxHexUtf8LengthFromMagnitudeByteCount(32, includeSign: true, include0xPrefix: true));
    }
}