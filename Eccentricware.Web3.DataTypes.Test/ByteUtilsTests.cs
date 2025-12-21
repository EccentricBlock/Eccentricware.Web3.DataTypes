using EccentricWare.Web3.DataTypes.Utils;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class ByteUtilsTests
{
    [TestMethod]
    public void ParseHexNibbleUtf8_ValidDigits_ReturnsExpected()
    {
        Assert.AreEqual(0, ByteUtils.ParseHexNibbleUtf8((byte)'0'));
        Assert.AreEqual(9, ByteUtils.ParseHexNibbleUtf8((byte)'9'));
        Assert.AreEqual(10, ByteUtils.ParseHexNibbleUtf8((byte)'a'));
        Assert.AreEqual(15, ByteUtils.ParseHexNibbleUtf8((byte)'f'));
        Assert.AreEqual(10, ByteUtils.ParseHexNibbleUtf8((byte)'A'));
        Assert.AreEqual(15, ByteUtils.ParseHexNibbleUtf8((byte)'F'));
    }

    [TestMethod]
    public void ParseHexNibbleUtf8_Invalid_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, ByteUtils.ParseHexNibbleUtf8((byte)'g'));
        Assert.AreEqual(-1, ByteUtils.ParseHexNibbleUtf8((byte)'_'));
        Assert.AreEqual(-1, ByteUtils.ParseHexNibbleUtf8(0));
    }

    [TestMethod]
    public void TryParseHexUInt64Utf8Variable_ParsesExpected()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Variable("ff"u8, out ulong v1));
        Assert.AreEqual(255UL, v1);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Variable("0001"u8, out ulong v2));
        Assert.AreEqual(1UL, v2);

        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Variable(ReadOnlySpan<byte>.Empty, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Variable("0123456789abcdef0"u8, out _)); // 17 nibbles
    }

    [TestMethod]
    public void TryWriteHexUInt64Minimal_WritesExpected()
    {
        Span<byte> buf = stackalloc byte[32];

        Assert.IsTrue(ByteUtils.TryWriteHexUInt64Minimal(0UL, buf, uppercase: false, out int w0));
        Assert.AreEqual(1, w0);
        Assert.AreEqual((byte)'0', buf[0]);

        Assert.IsTrue(ByteUtils.TryWriteHexUInt64Minimal(1UL, buf, uppercase: false, out int w1));
        Assert.AreEqual(1, w1);
        Assert.AreEqual((byte)'1', buf[0]);

        Assert.IsTrue(ByteUtils.TryWriteHexUInt64Minimal(16UL, buf, uppercase: false, out int w2));
        Assert.AreEqual(2, w2);
        Assert.AreEqual((byte)'1', buf[0]);
        Assert.AreEqual((byte)'0', buf[1]);
    }

    [TestMethod]
    public void WriteHexUInt64Fixed16_WritesExpectedLowerAndUpper()
    {
        const ulong value = 0x0123456789ABCDEFUL;

        Span<byte> lower = stackalloc byte[16];
        ByteUtils.WriteHexUInt64Fixed16(value, lower, uppercase: false);
        CollectionAssert.AreEqual("0123456789abcdef"u8.ToArray(), lower.ToArray());

        Span<byte> upper = stackalloc byte[16];
        ByteUtils.WriteHexUInt64Fixed16(value, upper, uppercase: true);
        CollectionAssert.AreEqual("0123456789ABCDEF"u8.ToArray(), upper.ToArray());
    }

    [TestMethod]
    public void Has0xPrefix_WorksForUtf8AndUtf16()
    {
        Assert.IsTrue(ByteUtils.Has0xPrefix("0x"u8));
        Assert.IsTrue(ByteUtils.Has0xPrefix("0X"u8));
        Assert.IsFalse(ByteUtils.Has0xPrefix("00"u8));

        Assert.IsTrue(ByteUtils.Has0xPrefix("0x".AsSpan()));
        Assert.IsTrue(ByteUtils.Has0xPrefix("0X".AsSpan()));
        Assert.IsFalse(ByteUtils.Has0xPrefix("00".AsSpan()));
    }
}
