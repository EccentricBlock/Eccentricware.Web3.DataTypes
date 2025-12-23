using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_UInt256ParsingTests
{
    [TestMethod]
    public void TryParseUInt256HexUtf8_EmptyIsZero()
    {
        Assert.IsTrue(ByteUtils.TryParseUInt256HexUtf8(ReadOnlySpan<byte>.Empty, out ulong u0, out ulong u1, out ulong u2, out ulong u3));
        Assert.AreEqual(0UL, u0);
        Assert.AreEqual(0UL, u1);
        Assert.AreEqual(0UL, u2);
        Assert.AreEqual(0UL, u3);
    }

    [TestMethod]
    public void TryParseUInt256HexUtf8_ValidVariousLengths()
    {
        AssertParseHexToLimbs_Utf8("1");
        AssertParseHexToLimbs_Utf8("f");
        AssertParseHexToLimbs_Utf8("0123");
        AssertParseHexToLimbs_Utf8("1234567890abcdef");
        AssertParseHexToLimbs_Utf8(new string('f', 64)); // 256-bit max
        AssertParseHexToLimbs_Utf8("0000000000000000000000000000000000000000000000000000000000000001");
    }

    [TestMethod]
    public void TryParseUInt256HexUtf8_RejectsOver64Digits_AndInvalid()
    {
        Assert.IsFalse(ByteUtils.TryParseUInt256HexUtf8(Encoding.ASCII.GetBytes(new string('1', 65)), out _, out _, out _, out _));
        Assert.IsFalse(ByteUtils.TryParseUInt256HexUtf8("0g"u8, out _, out _, out _, out _));
    }

    [TestMethod]
    public void TryParseUInt256HexChars_ValidVariousLengths()
    {
        AssertParseHexToLimbs_Chars("1");
        AssertParseHexToLimbs_Chars("f");
        AssertParseHexToLimbs_Chars("0123");
        AssertParseHexToLimbs_Chars("1234567890abcdef");
        AssertParseHexToLimbs_Chars(new string('f', 64));
    }

    [TestMethod]
    public void TryParseUInt256DecimalUtf8_ValidAndOverflow()
    {
        BigInteger max = (BigInteger.One << 256) - 1;
        BigInteger overflow = (BigInteger.One << 256);

        byte[] maxUtf8 = Encoding.ASCII.GetBytes(max.ToString(CultureInfo.InvariantCulture));
        byte[] overflowUtf8 = Encoding.ASCII.GetBytes(overflow.ToString(CultureInfo.InvariantCulture));

        Assert.IsTrue(ByteUtils.TryParseUInt256DecimalUtf8(maxUtf8, out ulong u0, out ulong u1, out ulong u2, out ulong u3));
        AssertLimbsEqual(ToLimbsLE(max), u0, u1, u2, u3);

        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalUtf8(overflowUtf8, out _, out _, out _, out _));
        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalUtf8(ReadOnlySpan<byte>.Empty, out _, out _, out _, out _));
        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalUtf8("12x3"u8, out _, out _, out _, out _));
    }

    [TestMethod]
    public void TryParseUInt256DecimalChars_ValidAndOverflow()
    {
        BigInteger max = (BigInteger.One << 256) - 1;
        BigInteger overflow = (BigInteger.One << 256);

        string maxStr = max.ToString(CultureInfo.InvariantCulture);
        string overflowStr = overflow.ToString(CultureInfo.InvariantCulture);

        Assert.IsTrue(ByteUtils.TryParseUInt256DecimalChars(maxStr.AsSpan(), out ulong u0, out ulong u1, out ulong u2, out ulong u3));
        AssertLimbsEqual(ToLimbsLE(max), u0, u1, u2, u3);

        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalChars(overflowStr.AsSpan(), out _, out _, out _, out _));
        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalChars(ReadOnlySpan<char>.Empty, out _, out _, out _, out _));
        Assert.IsFalse(ByteUtils.TryParseUInt256DecimalChars("12x3".AsSpan(), out _, out _, out _, out _));
    }

    private static void AssertParseHexToLimbs_Utf8(string hex)
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes(hex);
        Assert.IsTrue(ByteUtils.TryParseUInt256HexUtf8(utf8, out ulong u0, out ulong u1, out ulong u2, out ulong u3), $"Failed to parse: {hex}");

        BigInteger expected = BigIntegerFromHex(hex);
        AssertLimbsEqual(ToLimbsLE(expected), u0, u1, u2, u3);
    }

    private static void AssertParseHexToLimbs_Chars(string hex)
    {
        Assert.IsTrue(ByteUtils.TryParseUInt256HexChars(hex.AsSpan(), out ulong u0, out ulong u1, out ulong u2, out ulong u3), $"Failed to parse: {hex}");

        BigInteger expected = BigIntegerFromHex(hex);
        AssertLimbsEqual(ToLimbsLE(expected), u0, u1, u2, u3);
    }

    private static BigInteger BigIntegerFromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return BigInteger.Zero;

        // Convert to big-endian bytes (allow odd length)
        if ((hex.Length & 1) != 0)
            hex = "0" + hex;

        int byteLen = hex.Length / 2;
        byte[] bytes = new byte[byteLen];
        for (int i = 0; i < byteLen; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    private static ulong[] ToLimbsLE(BigInteger value)
    {
        byte[] le = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Resize(ref le, 32);

        ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(0, 8));
        ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(8, 8));
        ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(16, 8));
        ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(le.AsSpan(24, 8));

        return new[] { u0, u1, u2, u3 };
    }

    private static void AssertLimbsEqual(ulong[] expectedLE, ulong u0, ulong u1, ulong u2, ulong u3)
    {
        Assert.AreEqual(expectedLE[0], u0);
        Assert.AreEqual(expectedLE[1], u1);
        Assert.AreEqual(expectedLE[2], u2);
        Assert.AreEqual(expectedLE[3], u3);
    }
}