using System.Numerics;
using System.Text;
using UInt256 = EccentricWare.Web3.DataTypes.uint256;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class UInt256ParsingFormattingTests
{
    [TestMethod]
    public void TryParse_Hex_EvmQuantityBasic()
    {
        Assert.IsTrue(UInt256.TryParse("0x0".AsSpan(), provider: null, out var z));
        Assert.AreEqual(UInt256.Zero, z);

        Assert.IsTrue(UInt256.TryParse("0x1".AsSpan(), provider: null, out var one));
        Assert.AreEqual(UInt256.One, one);

        Assert.IsTrue(UInt256.TryParse("  \n\t0XfF  ".AsSpan(), provider: null, out var ff));
        Assert.AreEqual(new UInt256(255UL), ff);

        // Leading zeros: ISpanParsable path permits them.
        Assert.IsTrue(UInt256.TryParse("0x0001".AsSpan(), provider: null, out var v));
        Assert.AreEqual(UInt256.One, v);
    }

    [TestMethod]
    public void TryParse_Hex_InvalidInputs()
    {
        Assert.IsFalse(UInt256.TryParse("0x".AsSpan(), provider: null, out _)); // empty after prefix
        Assert.IsFalse(UInt256.TryParse("0xg".AsSpan(), provider: null, out _)); // invalid char
        Assert.IsFalse(UInt256.TryParse(("0x" + new string('1', 65)).AsSpan(), provider: null, out _)); // too long
        Assert.IsFalse(UInt256.TryParse("ff".AsSpan(), provider: null, out _)); // treated as decimal, invalid
    }

    [TestMethod]
    public void TryParse_Decimal_BasicAndWhitespace()
    {
        Assert.IsTrue(UInt256.TryParse("0".AsSpan(), provider: null, out var z));
        Assert.AreEqual(UInt256.Zero, z);

        Assert.IsTrue(UInt256.TryParse("  42  ".AsSpan(), provider: null, out var fortyTwo));
        Assert.AreEqual(new UInt256(42UL), fortyTwo);

        Assert.IsTrue(UInt256.TryParse("18446744073709551615".AsSpan(), provider: null, out var maxU64));
        Assert.AreEqual(new UInt256(ulong.MaxValue), maxU64);
    }

    [TestMethod]
    public void TryParse_Decimal_MaxValueAndOverflow()
    {
        BigInteger max = (BigInteger.One << 256) - 1;
        string maxDec = max.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsTrue(UInt256.TryParse(maxDec.AsSpan(), provider: null, out var parsedMax));
        Assert.AreEqual(UInt256.MaxValue, parsedMax);

        BigInteger overflow = (BigInteger.One << 256); // 2^256 (one past max)
        string overflowDec = overflow.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsFalse(UInt256.TryParse(overflowDec.AsSpan(), provider: null, out _));
    }

    [TestMethod]
    public void TryParse_Decimal_InvalidInputs()
    {
        Assert.IsFalse(UInt256.TryParse("+1".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("-1".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("12_34".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("   ".AsSpan(), provider: null, out _));
    }

    [TestMethod]
    public void TryFormat_DefaultAndHexVariants_Chars()
    {
        UInt256 one = UInt256.One;

        Span<char> buf = stackalloc char[80];

        Assert.IsTrue(one.TryFormat(buf, out int w0, format: ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0x1", new string(buf[..w0]));

        Assert.IsTrue(UInt256.Zero.TryFormat(buf, out int wz, format: ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0x0", new string(buf[..wz]));

        Assert.IsTrue(one.TryFormat(buf, out int w1, "x".AsSpan(), provider: null));
        Assert.AreEqual("1", new string(buf[..w1]));

        Assert.IsTrue(one.TryFormat(buf, out int w2, "0X".AsSpan(), provider: null));
        Assert.AreEqual("0X1", new string(buf[..w2]));

        Assert.IsTrue(one.TryFormat(buf, out int w3, "x64".AsSpan(), provider: null));
        string s64 = new string(buf[..w3]);
        Assert.AreEqual(64, s64.Length);
        Assert.IsTrue(s64.EndsWith("1", StringComparison.Ordinal));
        Assert.AreEqual(new string('0', 63) + "1", s64);

        Assert.IsTrue(one.TryFormat(buf, out int w4, "0x64".AsSpan(), provider: null));
        string s0x64 = new string(buf[..w4]);
        Assert.AreEqual(66, s0x64.Length);
        Assert.IsTrue(s0x64.StartsWith("0x", StringComparison.Ordinal));
        Assert.IsTrue(s0x64.EndsWith("1", StringComparison.Ordinal));
        Assert.AreEqual("0x" + new string('0', 63) + "1", s0x64);
    }

    [TestMethod]
    public void TryFormat_HexVariants_Utf8()
    {
        UInt256 value = new UInt256(0xDEADBEEFUL);

        Span<byte> utf8 = stackalloc byte[66];

        Assert.IsTrue(value.TryFormat(utf8, out int w0, ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0xdeadbeef", Encoding.ASCII.GetString(utf8[..w0]));

        Assert.IsTrue(value.TryFormat(utf8, out int w1, "X".AsSpan(), provider: null));
        Assert.AreEqual("DEADBEEF", Encoding.ASCII.GetString(utf8[..w1]));

        Assert.IsTrue(value.TryFormat(utf8, out int w2, "0x64".AsSpan(), provider: null));
        string s = Encoding.ASCII.GetString(utf8[..w2]);
        Assert.AreEqual(66, s.Length);
        Assert.IsTrue(s.StartsWith("0x", StringComparison.Ordinal));
        Assert.IsTrue(s.EndsWith("deadbeef", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToString_Default_IsEvmQuantity()
    {
        Assert.AreEqual("0x0", UInt256.Zero.ToString());
        Assert.AreEqual("0x1", UInt256.One.ToString());
        Assert.AreEqual("0x2a", new UInt256(42UL).ToString());
    }
}
