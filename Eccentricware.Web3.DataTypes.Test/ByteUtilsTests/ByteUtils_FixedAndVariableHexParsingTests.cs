using EccentricWare.Web3.DataTypes.Utils;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_FixedAndVariableHexParsingTests
{
    [TestMethod]
    public void TryParseHexUInt32Utf8Fixed8_ParsesCorrectly()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt32Utf8Fixed8("deadbeef"u8, out uint value));
        Assert.AreEqual(0xDEADBEEFu, value);

        Assert.IsTrue(ByteUtils.TryParseHexUInt32Utf8Fixed8("DEADBEEF"u8, out value));
        Assert.AreEqual(0xDEADBEEFu, value);
    }

    [TestMethod]
    public void TryParseHexUInt32Utf8Fixed8_Invalid()
    {
        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8Fixed8("deadbee"u8, out _));   // len 7
        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8Fixed8("deadbeef0"u8, out _)); // len 9
        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8Fixed8("deadbeeg"u8, out _));  // invalid char
    }

    [TestMethod]
    public void TryParseHexUInt32CharsFixed8_ParsesCorrectly()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt32CharsFixed8("deadbeef".AsSpan(), out uint value));
        Assert.AreEqual(0xDEADBEEFu, value);

        Assert.IsTrue(ByteUtils.TryParseHexUInt32CharsFixed8("DEADBEEF".AsSpan(), out value));
        Assert.AreEqual(0xDEADBEEFu, value);
    }

    [TestMethod]
    public void TryParseHexUInt32Utf8_Variable_ValidAndInvalid()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt32Utf8("f"u8, out uint v));
        Assert.AreEqual(0xFu, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt32Utf8("0f"u8, out v));
        Assert.AreEqual(0xFu, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt32Utf8("ffffffff"u8, out v));
        Assert.AreEqual(uint.MaxValue, v);

        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8(ReadOnlySpan<byte>.Empty, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8("123456789"u8, out _)); // >8 digits
        Assert.IsFalse(ByteUtils.TryParseHexUInt32Utf8("12x"u8, out _));
    }

    [TestMethod]
    public void TryParseHexUInt64Utf8Variable_ValidAndInvalid()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Variable("0"u8, out ulong v));
        Assert.AreEqual(0UL, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Variable("f"u8, out v));
        Assert.AreEqual(15UL, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Variable("ffffffffffffffff"u8, out v));
        Assert.AreEqual(ulong.MaxValue, v);

        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Variable(ReadOnlySpan<byte>.Empty, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Variable("10000000000000000"u8, out _)); // 17 digits
        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Variable("zz"u8, out _));
    }

    [TestMethod]
    public void TryParseHexUInt64CharsVariable_ValidAndInvalid()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt64CharsVariable("0".AsSpan(), out ulong v));
        Assert.AreEqual(0UL, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64CharsVariable("ffffffffffffffff".AsSpan(), out v));
        Assert.AreEqual(ulong.MaxValue, v);

        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsVariable(ReadOnlySpan<char>.Empty, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsVariable("10000000000000000".AsSpan(), out _)); // 17 digits
        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsVariable("12xz".AsSpan(), out _));
    }

    [TestMethod]
    public void TryParseHexUInt64CharsFixed16_ValidAndInvalid()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt64CharsFixed16("0000000000000000".AsSpan(), out ulong v));
        Assert.AreEqual(0UL, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64CharsFixed16("ffffffffffffffff".AsSpan(), out v));
        Assert.AreEqual(ulong.MaxValue, v);

        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsFixed16("fffffffffffffff".AsSpan(), out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsFixed16("fffffffffffffffff".AsSpan(), out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64CharsFixed16("fffffffffffffffg".AsSpan(), out _));
    }

    [TestMethod]
    public void TryParseHexUInt64Utf8Fixed16_ValidAndInvalid()
    {
        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Fixed16("0000000000000000"u8, out ulong v));
        Assert.AreEqual(0UL, v);

        Assert.IsTrue(ByteUtils.TryParseHexUInt64Utf8Fixed16("ffffffffffffffff"u8, out v));
        Assert.AreEqual(ulong.MaxValue, v);

        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Fixed16("fffffffffffffff"u8, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Fixed16("fffffffffffffffff"u8, out _));
        Assert.IsFalse(ByteUtils.TryParseHexUInt64Utf8Fixed16("fffffffffffffffg"u8, out _));
    }

    [TestMethod]
    public void ParseHexUInt64CharsVariable_ReturnsMaxValueOnInvalidOrWrongLength()
    {
        // Valid max parses to MaxValue (ambiguous sentinel design).
        Assert.AreEqual(ulong.MaxValue, ByteUtils.ParseHexUInt64CharsVariable("ffffffffffffffff".AsSpan()));

        // Invalid also returns MaxValue sentinel.
        Assert.AreEqual(ulong.MaxValue, ByteUtils.ParseHexUInt64CharsVariable("fffffffffffffffg".AsSpan()));
        Assert.AreEqual(ulong.MaxValue, ByteUtils.ParseHexUInt64CharsVariable(ReadOnlySpan<char>.Empty));
        Assert.AreEqual(ulong.MaxValue, ByteUtils.ParseHexUInt64CharsVariable("10000000000000000".AsSpan())); // 17 digits
    }
}