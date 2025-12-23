using EccentricWare.Web3.DataTypes.Utils;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_HexDecodingTests
{
    [TestMethod]
    public void GetHexDecodedByteCount_Behaviour()
    {
        Assert.AreEqual(-1, ByteUtils.GetHexDecodedByteCount(-1, allowOddLength: true));
        Assert.AreEqual(-1, ByteUtils.GetHexDecodedByteCount(3, allowOddLength: false));
        Assert.AreEqual(2, ByteUtils.GetHexDecodedByteCount(3, allowOddLength: true));
        Assert.AreEqual(2, ByteUtils.GetHexDecodedByteCount(4, allowOddLength: false));
    }

    [TestMethod]
    public void TryDecodeHexChars_EvenAndOdd()
    {
        Span<byte> dst = stackalloc byte[4];

        Assert.IsTrue(ByteUtils.TryDecodeHexChars("0a0b".AsSpan(), dst, out int written, allowOddLength: false));
        Assert.AreEqual(2, written);
        Assert.AreEqual(0x0A, dst[0]);
        Assert.AreEqual(0x0B, dst[1]);

        // Odd length allowed: "f" => 0x0f (low nibble only)
        dst.Clear();
        Assert.IsTrue(ByteUtils.TryDecodeHexChars("f".AsSpan(), dst, out written, allowOddLength: true));
        Assert.AreEqual(1, written);
        Assert.AreEqual(0x0F, dst[0]);

        // Odd length not allowed
        Assert.IsFalse(ByteUtils.TryDecodeHexChars("f".AsSpan(), dst, out _, allowOddLength: false));

        // Invalid
        Assert.IsFalse(ByteUtils.TryDecodeHexChars("0g".AsSpan(), dst, out _, allowOddLength: false));

        // Empty => true, 0 bytes
        Assert.IsTrue(ByteUtils.TryDecodeHexChars(ReadOnlySpan<char>.Empty, dst, out written, allowOddLength: true));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryDecodeHexUtf8_EvenAndOdd()
    {
        Span<byte> dst = stackalloc byte[4];

        Assert.IsTrue(ByteUtils.TryDecodeHexUtf8("0a0b"u8, dst, out int written, allowOddLength: false));
        Assert.AreEqual(2, written);
        Assert.AreEqual(0x0A, dst[0]);
        Assert.AreEqual(0x0B, dst[1]);

        dst.Clear();
        Assert.IsTrue(ByteUtils.TryDecodeHexUtf8("f"u8, dst, out written, allowOddLength: true));
        Assert.AreEqual(1, written);
        Assert.AreEqual(0x0F, dst[0]);

        Assert.IsFalse(ByteUtils.TryDecodeHexUtf8("f"u8, dst, out _, allowOddLength: false));
        Assert.IsFalse(ByteUtils.TryDecodeHexUtf8("0g"u8, dst, out _, allowOddLength: false));

        Assert.IsTrue(ByteUtils.TryDecodeHexUtf8(ReadOnlySpan<byte>.Empty, dst, out written, allowOddLength: true));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryParseHexByte_Fixed2_And_WriteHexByte_Fixed2()
    {
        Assert.IsTrue(ByteUtils.TryParseHexByteUtf8Fixed2("ff"u8, out byte b));
        Assert.AreEqual(0xFF, b);

        Assert.IsTrue(ByteUtils.TryParseHexByteCharsFixed2("0a".AsSpan(), out b));
        Assert.AreEqual(0x0A, b);

        Assert.IsFalse(ByteUtils.TryParseHexByteUtf8Fixed2("f"u8, out _));
        Assert.IsFalse(ByteUtils.TryParseHexByteCharsFixed2("0g".AsSpan(), out _));

        Span<char> c2 = stackalloc char[2];
        ByteUtils.WriteHexByteCharsFixed2(c2, 0xAB, uppercase: false);
        Assert.AreEqual("ab", c2.ToString());

        Span<byte> u2 = stackalloc byte[2];
        ByteUtils.WriteHexByteUtf8Fixed2(u2, 0xAB, ByteUtils.HexBytesUpper);
        Assert.AreEqual("AB", Encoding.ASCII.GetString(u2));
    }
}