using System;
using System.Numerics;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UInt256 = EccentricWare.Web3.DataTypes.uint256;

namespace EccentricWare.Web3.DataTypes.Tests.uint256;

/// <summary>
/// Core behavioural tests for <see cref="UInt256"/>:
/// - Construction and limb layout
/// - Equality, ordering, hashing
/// - Endianness encoding/decoding
/// - Shifts
/// - ISpanParsable parsing (hex/decimal)
/// - ISpanFormattable / IUtf8SpanFormattable formatting (hex variants; decimal via slow-path)
/// - Base-10 scaling helpers (pow10 fast path)
/// </summary>
/// <remarks>
/// Metadata tags: [core] [hotpath] [uint256] [format] [parse]
/// </remarks>
[TestClass]
public sealed class UInt256CoreTests
{
    private static readonly Random Rng = new Random(123456789);

    // ---------------------------
    // Construction / invariants
    // ---------------------------

    [TestMethod]
    public void Zero_IsDefault_AndIsZeroTrue()
    {
        Assert.AreEqual(default(UInt256), UInt256.Zero);
        Assert.IsTrue(UInt256.Zero.IsZero);
        Assert.IsFalse(UInt256.One.IsZero);
    }

    [TestMethod]
    public void Constructor_Ulong_SetsOnlyLowLimb()
    {
        const ulong v = 0xDEADBEEFCAFEBABEUL;
        UInt256 x = new UInt256(v);

        Assert.AreEqual(v, x.Limb0);
        Assert.AreEqual(0UL, x.Limb1);
        Assert.AreEqual(0UL, x.Limb2);
        Assert.AreEqual(0UL, x.Limb3);
    }

    [TestMethod]
    public void Constructor_Limbs_PreservesExactValues()
    {
        UInt256 x = new UInt256(
            limb0: 0x0102030405060708UL,
            limb1: 0x1112131415161718UL,
            limb2: 0x2122232425262728UL,
            limb3: 0x3132333435363738UL);

        Assert.AreEqual(0x0102030405060708UL, x.Limb0);
        Assert.AreEqual(0x1112131415161718UL, x.Limb1);
        Assert.AreEqual(0x2122232425262728UL, x.Limb2);
        Assert.AreEqual(0x3132333435363738UL, x.Limb3);
    }

    // ---------------------------
    // Equality / ordering / hash
    // ---------------------------

    [TestMethod]
    public void Equals_AndOperators_AreConsistent()
    {
        UInt256 a = new UInt256(123UL);
        UInt256 b = new UInt256(123UL);
        UInt256 c = new UInt256(124UL);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);

        Assert.IsFalse(a.Equals(c));
        Assert.IsTrue(a != c);
        Assert.IsFalse(a == c);
    }

    [TestMethod]
    public void CompareTo_OrdersByMostSignificantLimb()
    {
        UInt256 low = new UInt256(limb0: ulong.MaxValue, limb1: ulong.MaxValue, limb2: ulong.MaxValue, limb3: 0);
        UInt256 high = new UInt256(limb0: 0, limb1: 0, limb2: 0, limb3: 1);

        Assert.IsTrue(high > low);
        Assert.IsTrue(low < high);
        Assert.IsTrue(high >= low);
        Assert.IsTrue(low <= high);

        Assert.AreEqual(0, high.CompareTo(high));
        Assert.AreNotEqual(0, high.CompareTo(low));
    }

    [TestMethod]
    public void GetHashCode_EqualValuesHaveEqualHashCode()
    {
        UInt256 a = new UInt256(
            limb0: 1, limb1: 2, limb2: 3, limb3: 4);

        UInt256 b = new UInt256(
            limb0: 1, limb1: 2, limb2: 3, limb3: 4);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreEqual(a.GetStableHash64(), b.GetStableHash64());
    }

    [TestMethod]
    public void GetStableHash64_BasicNonCollisions_ForSmallSet()
    {
        // Not a cryptographic test; just a regression guard for obvious mistakes (e.g., ignoring limbs).
        UInt256 a = new UInt256(1UL);
        UInt256 b = new UInt256(2UL);
        UInt256 c = new UInt256(limb0: 0, limb1: 1, limb2: 0, limb3: 0);
        UInt256 d = new UInt256(limb0: 0, limb1: 0, limb2: 1, limb3: 0);

        Assert.AreNotEqual(a.GetStableHash64(), b.GetStableHash64());
        Assert.AreNotEqual(a.GetStableHash64(), c.GetStableHash64());
        Assert.AreNotEqual(c.GetStableHash64(), d.GetStableHash64());
    }

    // ---------------------------
    // Endianness
    // ---------------------------

    [TestMethod]
    public void WriteBigEndian_One_SetsLastByteOnly()
    {
        Span<byte> bytes = stackalloc byte[32];
        UInt256.One.WriteBigEndian(bytes);

        for (int i = 0; i < 31; i++)
            Assert.AreEqual((byte)0, bytes[i], $"Byte[{i}] must be zero.");

        Assert.AreEqual((byte)1, bytes[31]);
    }

    [TestMethod]
    public void WriteLittleEndian_One_SetsFirstByteOnly()
    {
        Span<byte> bytes = stackalloc byte[32];
        UInt256.One.WriteLittleEndian(bytes);

        Assert.AreEqual((byte)1, bytes[0]);
        for (int i = 1; i < 32; i++)
            Assert.AreEqual((byte)0, bytes[i], $"Byte[{i}] must be zero.");
    }

    [TestMethod]
    public void FromBigEndian32_RoundTrips_ForRandomValues()
    {
        for (int i = 0; i < 64; i++)
        {
            UInt256 x = NextRandomUInt256();

            Span<byte> bytes = stackalloc byte[32];
            x.WriteBigEndian(bytes);

            UInt256 y = UInt256.FromBigEndian32(bytes);
            Assert.AreEqual(x, y, $"Roundtrip mismatch at iteration {i}.");
        }
    }

    //[TestMethod]
    //public void FromBigEndian32_RejectsNon32ByteInput()
    //{
    //    Assert.ThrowsException<ArgumentException>(() => UInt256.FromBigEndian32(new byte[31]));
    //    Assert.ThrowsException<ArgumentException>(() => UInt256.FromBigEndian32(new byte[33]));
    //}

    // ---------------------------
    // Shifts
    // ---------------------------

    [TestMethod]
    public void ShiftLeftRight_WordBoundaryCases()
    {
        UInt256 one = UInt256.One;

        Assert.AreEqual(new UInt256(2UL), one << 1);
        Assert.AreEqual(one, (one << 1) >> 1);

        Assert.AreEqual(new UInt256(limb0: 0, limb1: 1, limb2: 0, limb3: 0), one << 64);
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 0, limb2: 1, limb3: 0), one << 128);
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 0, limb2: 0, limb3: 1), one << 192);

        Assert.AreEqual(UInt256.Zero, one << 256);
        Assert.AreEqual(UInt256.Zero, one >> 256);
    }

    [TestMethod]
    public void ShiftLeftRight_MatchesBigInteger_ForRandomShifts()
    {
        for (int i = 0; i < 64; i++)
        {
            UInt256 x = NextRandomUInt256();
            int shift = Rng.Next(0, 256);

            BigInteger bx = (BigInteger)x;
            BigInteger mask = (BigInteger.One << 256) - 1;

            UInt256 left = x << shift;
            BigInteger expectedLeft = (bx << shift) & mask;
            Assert.AreEqual(expectedLeft, (BigInteger)left, $"Left shift mismatch: shift={shift}.");

            UInt256 right = x >> shift;
            BigInteger expectedRight = bx >> shift;
            Assert.AreEqual(expectedRight, (BigInteger)right, $"Right shift mismatch: shift={shift}.");
        }
    }

    // ---------------------------
    // Parsing (ISpanParsable)
    // ---------------------------

    [TestMethod]
    public void TryParse_Hex_WithPrefix_ParsesCorrectly()
    {
        Assert.IsTrue(UInt256.TryParse("0x0".AsSpan(), provider: null, out var z));
        Assert.AreEqual(UInt256.Zero, z);

        Assert.IsTrue(UInt256.TryParse("0x1".AsSpan(), provider: null, out var one));
        Assert.AreEqual(UInt256.One, one);

        Assert.IsTrue(UInt256.TryParse("  \t\r\n0Xff  ".AsSpan(), provider: null, out var ff));
        Assert.AreEqual(new UInt256(255UL), ff);

        Assert.IsTrue(UInt256.TryParse("0x0001".AsSpan(), provider: null, out var padded));
        Assert.AreEqual(UInt256.One, padded);
    }

    [TestMethod]
    public void TryParse_Hex_TooLongOrInvalid_Fails()
    {
        Assert.IsFalse(UInt256.TryParse("0x".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("0xg".AsSpan(), provider: null, out _));

        string tooLong = "0x" + new string('1', 65);
        Assert.IsFalse(UInt256.TryParse(tooLong.AsSpan(), provider: null, out _));
    }

    [TestMethod]
    public void TryParse_Decimal_ParsesCorrectly()
    {
        Assert.IsTrue(UInt256.TryParse("0".AsSpan(), provider: null, out var z));
        Assert.AreEqual(UInt256.Zero, z);

        Assert.IsTrue(UInt256.TryParse("42".AsSpan(), provider: null, out var fortyTwo));
        Assert.AreEqual(new UInt256(42UL), fortyTwo);

        Assert.IsTrue(UInt256.TryParse("  18446744073709551615  ".AsSpan(), provider: null, out var maxU64));
        Assert.AreEqual(new UInt256(ulong.MaxValue), maxU64);
    }

    [TestMethod]
    public void TryParse_Decimal_Invalid_Fails()
    {
        Assert.IsFalse(UInt256.TryParse("".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("   ".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("+1".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("-1".AsSpan(), provider: null, out _));
        Assert.IsFalse(UInt256.TryParse("12_34".AsSpan(), provider: null, out _));
    }

    [TestMethod]
    public void TryParse_Decimal_MaxValue_Succeeds_OverflowFails()
    {
        BigInteger max = (BigInteger.One << 256) - 1;
        string maxDec = max.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsTrue(UInt256.TryParse(maxDec.AsSpan(), provider: null, out var parsedMax));
        Assert.AreEqual(UInt256.MaxValue, parsedMax);

        BigInteger overflow = (BigInteger.One << 256);
        string overflowDec = overflow.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsFalse(UInt256.TryParse(overflowDec.AsSpan(), provider: null, out _));
    }

    // ---------------------------
    // Formatting (ISpanFormattable / IUtf8SpanFormattable)
    // ---------------------------

    [TestMethod]
    public void TryFormat_Default_IsCanonicalEvmQuantity()
    {
        Span<char> buf = stackalloc char[80];

        Assert.IsTrue(UInt256.Zero.TryFormat(buf, out int wz, format: ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0x0", new string(buf[..wz]));

        Assert.IsTrue(UInt256.One.TryFormat(buf, out int wo, format: ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0x1", new string(buf[..wo]));

        Assert.IsTrue(new UInt256(42UL).TryFormat(buf, out int w42, format: ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0x2a", new string(buf[..w42]));
    }

    [TestMethod]
    public void TryFormat_HexVariants_Chars()
    {
        UInt256 v = new UInt256(0xDEADBEEFUL);
        Span<char> buf = stackalloc char[80];

        Assert.IsTrue(v.TryFormat(buf, out int w1, "x".AsSpan(), provider: null));
        Assert.AreEqual("deadbeef", new string(buf[..w1]));

        Assert.IsTrue(v.TryFormat(buf, out int w2, "X".AsSpan(), provider: null));
        Assert.AreEqual("DEADBEEF", new string(buf[..w2]));

        Assert.IsTrue(v.TryFormat(buf, out int w3, "0x".AsSpan(), provider: null));
        Assert.AreEqual("0xdeadbeef", new string(buf[..w3]));

        Assert.IsTrue(UInt256.One.TryFormat(buf, out int w4, "x64".AsSpan(), provider: null));
        string x64 = new string(buf[..w4]);
        Assert.AreEqual(64, x64.Length);
        Assert.AreEqual(new string('0', 63) + "1", x64);

        Assert.IsTrue(UInt256.One.TryFormat(buf, out int w5, "0x64".AsSpan(), provider: null));
        string ox64 = new string(buf[..w5]);
        Assert.AreEqual(66, ox64.Length);
        Assert.AreEqual("0x" + new string('0', 63) + "1", ox64);
    }

    [TestMethod]
    public void TryFormat_HexVariants_Utf8()
    {
        UInt256 v = new UInt256(0xDEADBEEFUL);

        Span<byte> buf = stackalloc byte[80];

        Assert.IsTrue(v.TryFormat(buf, out int w1, ReadOnlySpan<char>.Empty, provider: null));
        Assert.AreEqual("0xdeadbeef", Encoding.ASCII.GetString(buf[..w1]));

        Assert.IsTrue(v.TryFormat(buf, out int w2, "X".AsSpan(), provider: null));
        Assert.AreEqual("DEADBEEF", Encoding.ASCII.GetString(buf[..w2]));

        Assert.IsTrue(UInt256.One.TryFormat(buf, out int w3, "0x64".AsSpan(), provider: null));
        string s = Encoding.ASCII.GetString(buf[..w3]);
        Assert.AreEqual(66, s.Length);
        Assert.IsTrue(s.StartsWith("0x", StringComparison.Ordinal));
        Assert.IsTrue(s.EndsWith("1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TryFormat_UnknownFormat_ReturnsFalse()
    {
        Span<char> buf = stackalloc char[80];
        Assert.IsFalse(UInt256.One.TryFormat(buf, out _, "nope".AsSpan(), provider: null));

        Span<byte> utf8 = stackalloc byte[80];
        Assert.IsFalse(UInt256.One.TryFormat(utf8, out _, "nope".AsSpan(), provider: null));
    }

    [TestMethod]
    public void ToString_WithFormat_ProducesExpected()
    {
        UInt256 v = new UInt256(0xBADC0FFEE0DDF00DUL);

        Assert.AreEqual("0xbadc0ffee0ddf00d", v.ToString("0x", null));
        Assert.AreEqual("BADC0FFEE0DDF00D", v.ToString("X", null));
        Assert.AreEqual("badc0ffee0ddf00d", v.ToString("x", null));
    }

    // ---------------------------
    // Coin/base-unit scaling helpers
    // ---------------------------

    [TestMethod]
    public void TryScaleUpPow10_FastPath_ProducesExpected()
    {
        // 12345 * 10^6 = 12,345,000,000
        UInt256 v = new UInt256(12345UL);

        Assert.IsTrue(v.TryScaleUpPow10(decimalPlaces: 6, out var scaled));
        Assert.AreEqual(new UInt256(12_345_000_000UL), scaled);

        // Multiply up to 10^19 within ulong pow10 fast-path.
        UInt256 one = UInt256.One;
        Assert.IsTrue(one.TryScaleUpPow10(decimalPlaces: 19, out var scaled19));

        BigInteger expected = BigInteger.Pow(10, 19);
        Assert.AreEqual(expected, (BigInteger)scaled19);
    }

    [TestMethod]
    public void ScaleDownPow10Fast_ReturnsQuotientAndRemainder()
    {
        // 12345000000 / 10^6 = 12345 remainder 0
        UInt256 v = new UInt256(12_345_000_000UL);

        UInt256 q = v.ScaleDownPow10Fast(decimalPlaces: 6, out ulong r);
        Assert.AreEqual(new UInt256(12_345UL), q);
        Assert.AreEqual(0UL, r);

        // 123456789 / 10^3 = 123456 remainder 789
        UInt256 v2 = new UInt256(123_456_789UL);
        UInt256 q2 = v2.ScaleDownPow10Fast(decimalPlaces: 3, out ulong r2);
        Assert.AreEqual(new UInt256(123_456UL), q2);
        Assert.AreEqual(789UL, r2);
    }

    //[TestMethod]
    //public void ScaleDownPow10Fast_DecimalsGreaterThan19_Throws()
    //{
    //    // Fast-path explicitly limited to 0..19 in the provided implementation.
    //    Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
    //    {
    //        _ = UInt256.One.ScaleDownPow10Fast(decimalPlaces: 20, out _);
    //    });
    //}

    // ---------------------------
    // Helpers
    // ---------------------------

    private static UInt256 NextRandomUInt256()
    {
        // Build random limbs deterministically.
        ulong u0 = NextUInt64();
        ulong u1 = NextUInt64();
        ulong u2 = NextUInt64();
        ulong u3 = NextUInt64();
        return new UInt256(u0, u1, u2, u3);
    }

    private static ulong NextUInt64()
    {
        // Random.NextInt64 exists in newer frameworks; use bytes for determinism across targets.
        Span<byte> b = stackalloc byte[8];
        Rng.NextBytes(b);
        return BitConverter.ToUInt64(b);
    }
}
