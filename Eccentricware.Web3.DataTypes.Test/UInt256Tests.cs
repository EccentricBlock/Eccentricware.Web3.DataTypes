using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class UInt256Tests
{
    private static readonly BigInteger Mod256 = BigInteger.One << 256;
    private static readonly BigInteger Mask256 = (BigInteger.One << 256) - 1;
    private static readonly BigInteger TwoPow256 = BigInteger.One << 256;

    private static readonly string HexMaxValue64Lower = new string('f', 64);
    private static readonly string HexMaxValue64Upper = new string('F', 64);


    private static BigInteger ToBig(uint256 v) => v.ToBigInteger();

    private static uint256 FromBig(BigInteger v) => (uint256)v;

    private static byte[] HexToBytes(string hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (hex.Length == 0)
            return Array.Empty<byte>();

        if ((hex.Length & 1) != 0)
            hex = "0" + hex;

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.AsSpan(i * 2, 2).ToString(), 16);

        return bytes;
    }

    private static uint256 RandomUInt256(Random rng)
    {
        byte[] be = new byte[32];
        rng.NextBytes(be);
        return new uint256(be);
    }

    private static BigInteger RandomBig256(Random rng)
    {
        byte[] be = new byte[32];
        rng.NextBytes(be);
        return new BigInteger(be, isUnsigned: true, isBigEndian: true);
    }

    [TestMethod]
    public void Constructors_FromU64_And_Limbs_SetExpectedProperties()
    {
        uint256 a = new uint256(0UL);
        Assert.IsTrue(a.IsZero);
        Assert.IsFalse(a.IsOne);
        Assert.IsTrue(a.FitsInUlong);
        Assert.IsTrue(a.FitsInUInt128);
        Assert.AreEqual(0UL, a.Low64);

        uint256 b = new uint256(1UL);
        Assert.IsFalse(b.IsZero);
        Assert.IsTrue(b.IsOne);
        Assert.IsTrue(b.FitsInUlong);
        Assert.IsTrue(b.FitsInUInt128);
        Assert.AreEqual(1UL, b.Low64);

        uint256 c = new uint256(ulong.MaxValue, 1UL, 2UL, 3UL);
        Assert.IsFalse(c.FitsInUlong);
        Assert.IsFalse(c.FitsInUInt128);

        UInt128 expectedLow = ((UInt128)1UL << 64) | ulong.MaxValue;
        UInt128 expectedHigh = ((UInt128)3UL << 64) | 2UL;
        Assert.AreEqual(expectedLow, c.Low128);
        Assert.AreEqual(expectedHigh, c.High128);
        Assert.AreEqual(ulong.MaxValue, c.Low64);
    }

    [TestMethod]
    public void Constructor_FromBigEndianBytes_PadsLeftAndRejectsOver32()
    {
        // Empty => zero
        Assert.AreEqual(uint256.Zero, new uint256(ReadOnlySpan<byte>.Empty));

        // 1-byte big-endian { 0x01 } => 1
        Assert.AreEqual(uint256.One, new uint256(new byte[] { 0x01 }));

        // 3-byte 0x010203 => value
        uint256 v = new uint256(new byte[] { 0x01, 0x02, 0x03 });
        Assert.AreEqual(new BigInteger(new byte[] { 0x01, 0x02, 0x03 }, isUnsigned: true, isBigEndian: true), ToBig(v));

        // Exactly 32 bytes round-trip via ToBigEndianBytes
        byte[] be32 = new byte[32];
        for (int i = 0; i < 32; i++) be32[i] = (byte)i;
        uint256 x = new uint256(be32);
        CollectionAssert.AreEqual(be32, x.ToBigEndianBytes());

        Assert.Throws<ArgumentException>(() => _ = new uint256(new byte[33]));
    }

    [TestMethod]
    public void FromLittleEndian_PadsRightAndRejectsOver32()
    {
        Assert.AreEqual(uint256.Zero, uint256.FromLittleEndian(ReadOnlySpan<byte>.Empty));

        // little-endian {0x01} => 1
        Assert.AreEqual(uint256.One, uint256.FromLittleEndian(new byte[] { 0x01 }));

        // little-endian {0x03,0x02,0x01} => 0x010203
        uint256 v = uint256.FromLittleEndian(new byte[] { 0x03, 0x02, 0x01 });
        Assert.AreEqual(new BigInteger(new byte[] { 0x01, 0x02, 0x03 }, isUnsigned: true, isBigEndian: true), ToBig(v));

        Assert.Throws<ArgumentException>(() => _ = uint256.FromLittleEndian(new byte[33]));
    }

    [TestMethod]
    public void WriteBigEndian_WriteLittleEndian_ThrowIfDestinationTooSmall()
    {
        uint256 v = new uint256(1UL);

        Assert.Throws<ArgumentException>(() => v.WriteBigEndian(new byte[31]));
        Assert.Throws<ArgumentException>(() => v.WriteLittleEndian(new byte[31]));
    }

    [TestMethod]
    public void Endian_Write_RoundTrips()
    {
        uint256 v = new uint256(0x1122334455667788UL, 0x99AABBCCDDEEFF00UL, 0x0123456789ABCDEFUL, 0x0FEDCBA987654321UL);

        Span<byte> be = stackalloc byte[32];
        Span<byte> le = stackalloc byte[32];

        v.WriteBigEndian(be);
        v.WriteLittleEndian(le);

        uint256 fromBe = new uint256(be);
        uint256 fromLe = uint256.FromLittleEndian(le);

        Assert.AreEqual(v, fromBe);
        Assert.AreEqual(v, fromLe);
    }

    [TestMethod]
    public void GetByteCount_And_ToMinimalBigEndianBytes_AgreeWithBigInteger()
    {
        Assert.AreEqual(0, uint256.Zero.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 0 }, uint256.Zero.ToMinimalBigEndianBytes());

        uint256 one = uint256.One;
        Assert.AreEqual(1, one.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 1 }, one.ToMinimalBigEndianBytes());

        uint256 v = new uint256(new byte[] { 0x01, 0x02, 0x03 });
        Assert.AreEqual(3, v.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, v.ToMinimalBigEndianBytes());

        // Randomised cross-check
        var rng = new Random(12345);
        for (int i = 0; i < 200; i++)
        {
            uint256 r = RandomUInt256(rng);
            BigInteger bi = ToBig(r);

            int expected = bi.IsZero ? 0 : bi.GetByteCount(isUnsigned: true);
            Assert.AreEqual(expected, r.GetByteCount(), $"Mismatch byte count at iteration {i}.");

            byte[] minimal = r.ToMinimalBigEndianBytes();
            if (bi.IsZero)
            {
                CollectionAssert.AreEqual(new byte[] { 0 }, minimal);
            }
            else
            {
                Assert.HasCount(expected, minimal);
                Assert.AreNotEqual(0, minimal[0], "Minimal encoding must not have leading zeros.");
                BigInteger roundTrip = new BigInteger(minimal, isUnsigned: true, isBigEndian: true);
                Assert.AreEqual(bi, roundTrip);
            }
        }
    }

    [TestMethod]
    public void Equality_Operators_And_HashCode_Consistency()
    {
        uint256 a = new uint256(1UL, 2UL, 3UL, 4UL);
        uint256 b = new uint256(1UL, 2UL, 3UL, 4UL);
        uint256 c = new uint256(1UL, 2UL, 3UL, 5UL);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);

        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a == c);
        Assert.IsTrue(a != c);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "Equal values must have equal hash codes.");
        Assert.IsFalse(a.Equals((object)123));
        Assert.IsTrue(a.Equals((object)b));
    }

    [TestMethod]
    public void Comparison_And_CompareToObject_Behaviour()
    {
        uint256 a = new uint256(0UL, 0UL, 0UL, 0UL);
        uint256 b = new uint256(1UL, 0UL, 0UL, 0UL);
        uint256 c = new uint256(0UL, 1UL, 0UL, 0UL);
        uint256 d = new uint256(0UL, 0UL, 1UL, 0UL);
        uint256 e = new uint256(0UL, 0UL, 0UL, 1UL);

        Assert.IsTrue(a < b);
        Assert.IsTrue(b < c);
        Assert.IsTrue(c < d);
        Assert.IsTrue(d < e);

        Assert.AreEqual(0, c.CompareTo(c));
        Assert.AreEqual(1, c.CompareTo((object?)null));
        Assert.Throws<ArgumentException>(() => c.CompareTo((object)"not-a-uint256"));
    }

    [TestMethod]
    public void Addition_Checked_And_Unchecked_Wrap()
    {
        Assert.AreEqual(uint256.Zero, uint256.AddUnchecked(uint256.MaxValue, uint256.One));

        Assert.Throws<OverflowException>(() =>
        {
            _ = uint256.MaxValue + uint256.One;
        });

        // Randomised checked addition against BigInteger
        var rng = new Random(222);
        for (int i = 0; i < 200; i++)
        {
            uint256 x = RandomUInt256(rng);
            uint256 y = RandomUInt256(rng);

            BigInteger bx = ToBig(x);
            BigInteger by = ToBig(y);
            BigInteger sum = bx + by;

            if (sum >= Mod256)
            {
                Assert.Throws<OverflowException>(() => _ = x + y, $"Expected overflow at iteration {i}.");
                uint256 wrapped = uint256.AddUnchecked(x, y);
                Assert.AreEqual(sum & Mask256, ToBig(wrapped));
            }
            else
            {
                uint256 z = x + y;
                Assert.AreEqual(sum, ToBig(z));
                uint256 z2 = uint256.AddUnchecked(x, y);
                Assert.AreEqual(sum, ToBig(z2));
            }
        }
    }

    [TestMethod]
    public void Subtraction_Checked_And_Unchecked_Wrap()
    {
        Assert.AreEqual(uint256.MaxValue, uint256.SubtractUnchecked(uint256.Zero, uint256.One));

        Assert.Throws<OverflowException>(() =>
        {
            _ = uint256.Zero - uint256.One;
        });

        var rng = new Random(333);
        for (int i = 0; i < 200; i++)
        {
            uint256 x = RandomUInt256(rng);
            uint256 y = RandomUInt256(rng);

            BigInteger bx = ToBig(x);
            BigInteger by = ToBig(y);

            if (bx < by)
            {
                Assert.Throws<OverflowException>(() => _ = x - y, $"Expected underflow at iteration {i}.");
                uint256 wrapped = uint256.SubtractUnchecked(x, y);
                Assert.AreEqual((bx - by) & Mask256, ToBig(wrapped));
            }
            else
            {
                uint256 z = x - y;
                Assert.AreEqual(bx - by, ToBig(z));
                uint256 z2 = uint256.SubtractUnchecked(x, y);
                Assert.AreEqual(bx - by, ToBig(z2));
            }
        }
    }

    [TestMethod]
    public void Multiplication_Checked_And_Unchecked_Wrap()
    {
        Assert.Throws<OverflowException>(() =>
        {
            _ = uint256.MaxValue * new uint256(2UL);
        });

        uint256 wrapped = uint256.MultiplyUnchecked(uint256.MaxValue, new uint256(2UL));
        Assert.AreEqual((ToBig(uint256.MaxValue) * 2) & Mask256, ToBig(wrapped));

        var rng = new Random(444);
        for (int i = 0; i < 150; i++)
        {
            uint256 x = RandomUInt256(rng);
            uint256 y = RandomUInt256(rng);

            BigInteger prod = ToBig(x) * ToBig(y);

            if (prod >= Mod256)
            {
                Assert.Throws<OverflowException>(() => _ = x * y, $"Expected overflow at iteration {i}.");
                Assert.AreEqual(prod & Mask256, ToBig(uint256.MultiplyUnchecked(x, y)));
            }
            else
            {
                Assert.AreEqual(prod, ToBig(x * y));
                Assert.AreEqual(prod, ToBig(uint256.MultiplyUnchecked(x, y)));
            }
        }
    }

    [TestMethod]
    public void Division_Modulo_And_DivRem_Correctness_FastAndColdPaths()
    {
        Assert.Throws<DivideByZeroException>(() => _ = uint256.One / uint256.Zero);
        Assert.Throws<DivideByZeroException>(() => _ = uint256.One % uint256.Zero);
        Assert.Throws<DivideByZeroException>(() => _ = uint256.DivRem(uint256.One, uint256.Zero));

        // Fast path: 64-bit divisor (u3=u2=u1=0)
        uint256 a = uint256.Parse("0x1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        uint256 d64 = new uint256(97UL);
        uint256 q = a / d64;
        uint256 r = a % d64;

        BigInteger ba = ToBig(a);
        BigInteger bd64 = 97;
        Assert.AreEqual(ba / bd64, ToBig(q));
        Assert.AreEqual(ba % bd64, ToBig(r));

        var (q2, r2) = uint256.DivRem(a, d64);
        Assert.AreEqual(q, q2);
        Assert.AreEqual(r, r2);

        // Cold path: divisor with higher limbs set (e.g. 2^64)
        uint256 dCold = new uint256(0UL, 1UL, 0UL, 0UL); // 2^64
        uint256 qC = a / dCold;
        uint256 rC = a % dCold;

        BigInteger bdCold = BigInteger.One << 64;
        Assert.AreEqual(ba / bdCold, ToBig(qC));
        Assert.AreEqual(ba % bdCold, ToBig(rC));

        var (qC2, rC2) = uint256.DivRem(a, dCold);
        Assert.AreEqual(qC, qC2);
        Assert.AreEqual(rC, rC2);
    }

    [TestMethod]
    public void TryMultiply_And_DivRemU64_AgreeWithBigInteger()
    {
        uint256 max = uint256.MaxValue;
        Assert.IsFalse(max.TryMultiply(2UL, out uint256 overflowed));
        Assert.AreEqual(uint256.Zero, overflowed);

        uint256 v = uint256.Parse("0x1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        Assert.IsTrue(v.TryMultiply(10UL, out uint256 scaled));
        Assert.AreEqual(ToBig(v) * 10, ToBig(scaled));

        ulong divisor = 1000UL;
        uint256 q = v.DivRem(divisor, out ulong rem);

        BigInteger bv = ToBig(v);
        Assert.AreEqual(bv / divisor, ToBig(q));
        Assert.AreEqual((ulong)(bv % divisor), rem);
    }

    [TestMethod]
    public void Pow10U64_ValidRange_And_ThrowsOutOfRange()
    {
        Assert.AreEqual(1UL, uint256.Pow10U64(0));
        Assert.AreEqual(10UL, uint256.Pow10U64(1));
        Assert.AreEqual(1_000_000_000_000_000_000UL, uint256.Pow10U64(18));
        Assert.AreEqual(10_000_000_000_000_000_000UL, uint256.Pow10U64(19));

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.Pow10U64(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.Pow10U64(20));
    }

    [TestMethod]
    public void UnaryNegation_Increment_Decrement_Semantics()
    {
        Assert.AreEqual(uint256.Zero, -uint256.Zero);
        Assert.AreEqual(uint256.MaxValue, -uint256.One);

        uint256 z = uint256.Zero;
        Assert.AreEqual(uint256.One, ++z);

        Assert.Throws<OverflowException>(() =>
        {
            uint256 z = uint256.Zero;
            _ = --z;
        });
    }

    [TestMethod]
    public void BitwiseOps_And_Shifts_MatchBigIntegerMasking()
    {
        var rng = new Random(555);

        for (int i = 0; i < 150; i++)
        {
            uint256 x = RandomUInt256(rng);
            uint256 y = RandomUInt256(rng);

            BigInteger bx = ToBig(x);
            BigInteger by = ToBig(y);

            Assert.AreEqual((bx & by) & Mask256, ToBig(x & y));
            Assert.AreEqual((bx | by) & Mask256, ToBig(x | y));
            Assert.AreEqual((bx ^ by) & Mask256, ToBig(x ^ y));
            Assert.AreEqual((~bx) & Mask256, ToBig(~x));

            int s = rng.Next(0, 300);
            uint256 sl = x << s;
            uint256 sr = x >> s;

            BigInteger expectedSl = s >= 256 ? BigInteger.Zero : ((bx << s) & Mask256);
            BigInteger expectedSr = s >= 256 ? BigInteger.Zero : (bx >> s);

            Assert.AreEqual(expectedSl, ToBig(sl), $"Left shift mismatch at iteration {i}, shift {s}.");
            Assert.AreEqual(expectedSr, ToBig(sr), $"Right shift mismatch at iteration {i}, shift {s}.");
        }
    }

    [TestMethod]
    public void BitCounts_AreCorrect()
    {
        Assert.AreEqual(256, uint256.Zero.LeadingZeroCount());
        Assert.AreEqual(256, uint256.Zero.TrailingZeroCount());
        Assert.AreEqual(0, uint256.Zero.PopCount());

        Assert.AreEqual(255, uint256.One.LeadingZeroCount());
        Assert.AreEqual(0, uint256.One.TrailingZeroCount());
        Assert.AreEqual(1, uint256.One.PopCount());

        Assert.AreEqual(0, uint256.MaxValue.LeadingZeroCount());
        Assert.AreEqual(0, uint256.MaxValue.TrailingZeroCount());
        Assert.AreEqual(256, uint256.MaxValue.PopCount());

        // Cross-check vs BigInteger for random values
        var rng = new Random(666);
        for (int i = 0; i < 200; i++)
        {
            uint256 x = RandomUInt256(rng);
            BigInteger bx = ToBig(x);

            int lz = x.LeadingZeroCount();
            int tz = x.TrailingZeroCount();
            int pc = x.PopCount();

            // Leading/trailing for BigInteger (manual via bytes) to avoid sign issues
            Span<byte> be = stackalloc byte[32];
            x.WriteBigEndian(be);

            int expectedLz = 0;
            bool any = false;
            for (int b = 0; b < 32; b++)
            {
                byte v = be[b];
                if (v == 0 && !any)
                {
                    expectedLz += 8;
                    continue;
                }
                if (!any)
                {
                    any = true;
                    expectedLz += BitOperations.LeadingZeroCount(v) - 24; // byte-leading-zero-count
                }
            }
            if (!any) expectedLz = 256;

            // Trailing zeros: check from least-significant byte
            int expectedTz = 0;
            any = false;
            for (int b = 31; b >= 0; b--)
            {
                byte v = be[b];
                if (v == 0 && !any)
                {
                    expectedTz += 8;
                    continue;
                }
                if (!any)
                {
                    any = true;
                    expectedTz += BitOperations.TrailingZeroCount(v);
                }
            }
            if (!any) expectedTz = 256;

            int expectedPc = 0;
            for (int b = 0; b < 32; b++)
                expectedPc += BitOperations.PopCount(be[b]);

            Assert.AreEqual(expectedLz, lz, $"LeadingZeroCount mismatch at iteration {i} (BigInteger={bx}).");
            Assert.AreEqual(expectedTz, tz, $"TrailingZeroCount mismatch at iteration {i} (BigInteger={bx}).");
            Assert.AreEqual(expectedPc, pc, $"PopCount mismatch at iteration {i} (BigInteger={bx}).");
        }
    }

    [TestMethod]
    public void Conversions_Ulong_UInt128_BigInteger_WorkAndEnforceRange()
    {
        uint256 a = new uint256(42UL);
        Assert.AreEqual(42UL, (ulong)a);

        uint256 b = new uint256(0UL, 1UL, 0UL, 0UL); // > ulong
        Assert.Throws<OverflowException>(() => _ = (ulong)b);


        uint256 c = new uint256(0UL, 0UL, 1UL, 0UL); // > UInt128
        Assert.Throws<OverflowException>(() => _ = (UInt128)c);
        // BigInteger round-trip (non-negative and <=256 bits)
        BigInteger bi = BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture);
        uint256 u = FromBig(bi);
        Assert.AreEqual(bi, ToBig(u));

        Assert.Throws<OverflowException>(() => _ = FromBig(new BigInteger(-1)));

        BigInteger tooLarge = BigInteger.One << 256;
        Assert.Throws<OverflowException>(() => _ = FromBig(tooLarge));
    }

    [TestMethod]
    public void Parsing_HexAndDecimal_Chars_SucceedsAndRejectsInvalid()
    {
        Assert.IsTrue(uint256.TryParse("0x".AsSpan(), out var z0x));
        Assert.AreEqual(uint256.Zero, z0x);

        Assert.IsTrue(uint256.TryParse("  0x2a  ".AsSpan(), out var h));
        Assert.AreEqual(new uint256(42UL), h);

        Assert.IsTrue(uint256.TryParse("42".AsSpan(), CultureInfo.InvariantCulture, out var d));
        Assert.AreEqual(new uint256(42UL), d);

        Assert.IsTrue(uint256.TryParseDecimal("  00042  ".AsSpan(), out var d2));
        Assert.AreEqual(new uint256(42UL), d2);

        Assert.IsFalse(uint256.TryParse("".AsSpan(), out _));
        Assert.IsFalse(uint256.TryParse("   ".AsSpan(), out _));
        Assert.IsFalse(uint256.TryParse("0xGG".AsSpan(), out _));
        Assert.IsFalse(uint256.TryParse("not-a-number".AsSpan(), out _));

        // Reject > 64 hex digits
        string tooLongHex = "0x" + new string('f', 65);
        Assert.IsFalse(uint256.TryParse(tooLongHex.AsSpan(), out _));

        // Max value (hex) parses
        Assert.IsTrue(uint256.TryParse(("0x" + new string('f', 64)).AsSpan(), out var max));
        Assert.AreEqual(uint256.MaxValue, max);

        // Max value (decimal) parses
        string maxDec = ToBig(uint256.MaxValue).ToString(CultureInfo.InvariantCulture);
        Assert.IsTrue(uint256.TryParse(maxDec.AsSpan(), CultureInfo.InvariantCulture, out var max2));
        Assert.AreEqual(uint256.MaxValue, max2);
    }

    [TestMethod]
    public void Parsing_Utf8_JsonRpcTokens_SupportsQuotesWhitespaceHexAndDecimal()
    {
        static ReadOnlySpan<byte> Utf8(string s) => Encoding.UTF8.GetBytes(s);

        Assert.IsTrue(uint256.TryParse(Utf8("\"0x2a\""), out var a));
        Assert.AreEqual(new uint256(42UL), a);

        Assert.IsTrue(uint256.TryParse(Utf8("  \"  0x2A  \"  "), out var b));
        Assert.AreEqual(new uint256(42UL), b);

        Assert.IsTrue(uint256.TryParse(Utf8("42"), out var c));
        Assert.AreEqual(new uint256(42UL), c);

        Assert.IsTrue(uint256.TryParse(Utf8("\"42\""), out var d));
        Assert.AreEqual(new uint256(42UL), d);

        Assert.IsTrue(uint256.TryParse(Utf8("0x"), out var z));
        Assert.AreEqual(uint256.Zero, z);

        Assert.IsFalse(uint256.TryParse(Utf8(""), out _));
        Assert.IsFalse(uint256.TryParse(Utf8("\"\""), out _));
        Assert.IsFalse(uint256.TryParse(Utf8("\"0xGG\""), out _));

        // Reject > 64 hex digits
        Assert.IsFalse(uint256.TryParse(Utf8("\"0x" + new string('f', 65) + "\""), out _));
    }

    [TestMethod]
    public void Formatting_ToString_And_TryFormat_HexVariants_And_Decimal()
    {
        uint256 z = uint256.Zero;
        Assert.AreEqual("0x0", z.ToString());
        Assert.AreEqual("0", z.ToString("x", CultureInfo.InvariantCulture));
        Assert.AreEqual("0", z.ToString("X", CultureInfo.InvariantCulture));
        Assert.AreEqual("0", z.ToString("d", CultureInfo.InvariantCulture));

        uint256 v = new uint256(0UL, 1UL, 0UL, 0UL); // 2^64
        Assert.AreEqual("0x10000000000000000", v.ToString("0x", CultureInfo.InvariantCulture));
        Assert.AreEqual("10000000000000000", v.ToString("x", CultureInfo.InvariantCulture));
        Assert.AreEqual("10000000000000000", v.ToString("X", CultureInfo.InvariantCulture)); // digits only
        Assert.AreEqual((BigInteger.One << 64).ToString(CultureInfo.InvariantCulture), v.ToString("d", CultureInfo.InvariantCulture));

        // Full-width helper is always 64 chars, no prefix
        string full = uint256.One.ToFullHexString();
        Assert.AreEqual(64, full.Length);
        Assert.IsTrue(full.EndsWith("1", StringComparison.Ordinal));
        Assert.AreEqual(0, full[..63].Trim('0').Length, "All leading chars should be zeros for value 1.");

        Assert.Throws<FormatException>(() => _ = v.ToString("nope", CultureInfo.InvariantCulture));

        // TryFormat to char buffer
        Span<char> dst = stackalloc char[66];
        Assert.IsTrue(v.TryFormat(dst, out int written, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("0x10000000000000000", new string(dst[..written]));

        // TryFormat to UTF8 buffer
        Span<byte> u8 = stackalloc byte[66];
        Assert.IsTrue(v.TryFormat(u8, out int bw, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("0x10000000000000000", Encoding.ASCII.GetString(u8[..bw]));
    }

    [TestMethod]
    public void TryFormat_FailsWhenDestinationTooSmall_And_DoesNotOverrun()
    {
        uint256 v = uint256.MaxValue;

        Span<char> small = stackalloc char[1];
        Assert.IsFalse(v.TryFormat(small, out int cw, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(0, cw);

        Span<byte> small8 = stackalloc byte[1];
        Assert.IsFalse(v.TryFormat(small8, out int bw, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(0, bw);
    }

    [TestMethod]
    public void AbiEncoding_RoundTrip_And_LengthValidation()
    {
        uint256 v = uint256.Parse("0x1234".AsSpan(), CultureInfo.InvariantCulture);
        byte[] abi = v.ToAbiEncoded();
        Assert.HasCount(32, abi);

        uint256 rt = uint256.FromAbiEncoded(abi);
        Assert.AreEqual(v, rt);

        Assert.Throws<ArgumentException>(() => _ = uint256.FromAbiEncoded(new byte[31]));
    }

    [TestMethod]
    public void EvmAndSolanaConstants_AreCorrect()
    {
        // 10^18
        BigInteger ten18 = BigInteger.Pow(10, 18);
        Assert.AreEqual(ten18, ToBig(uint256.Evm.WeiPerEther));
        Assert.AreEqual(new BigInteger(1_000_000_000UL), ToBig(uint256.Evm.WeiPerGwei));

        // 10^9
        Assert.AreEqual(new BigInteger(1_000_000_000UL), ToBig(uint256.Solana.LamportsPerSol));
    }

    [TestMethod]
    public void JsonRoundTrip_UsesRegisteredConverter()
    {
        // This is intentionally a round-trip test to avoid over-constraining the converter's exact string shape.
        uint256 v = uint256.Parse("0x1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);

        string json = JsonSerializer.Serialize(v);
        uint256 rt = JsonSerializer.Deserialize<uint256>(json);

        Assert.AreEqual(v, rt);

        // Common JSON-RPC numeric-as-string pattern
        uint256 parsed = JsonSerializer.Deserialize<uint256>("\"0x2a\"");
        Assert.AreEqual(new uint256(42UL), parsed);
    }

    [TestMethod]
    public void Randomised_CoreOperations_Against_BigInteger()
    {
        var rng = new Random(777);

        for (int i = 0; i < 200; i++)
        {
            uint256 x = RandomUInt256(rng);
            uint256 y = RandomUInt256(rng);
            BigInteger bx = ToBig(x);
            BigInteger by = ToBig(y);

            // Unchecked wrap identities
            Assert.AreEqual(((bx + by) & Mask256), ToBig(uint256.AddUnchecked(x, y)));
            Assert.AreEqual(((bx - by) & Mask256), ToBig(uint256.SubtractUnchecked(x, y)));
            Assert.AreEqual(((bx * by) & Mask256), ToBig(uint256.MultiplyUnchecked(x, y)));

            // Division/mod when divisor != 0
            if (!y.IsZero)
            {
                Assert.AreEqual(bx / by, ToBig(x / y));
                Assert.AreEqual(bx % by, ToBig(x % y));

                var (q, r) = uint256.DivRem(x, y);
                Assert.AreEqual(bx / by, ToBig(q));
                Assert.AreEqual(bx % by, ToBig(r));
            }

            // Endian round-trips
            Span<byte> be = stackalloc byte[32];
            Span<byte> le = stackalloc byte[32];
            x.WriteBigEndian(be);
            x.WriteLittleEndian(le);

            Assert.AreEqual(x, new uint256(be));
            Assert.AreEqual(x, uint256.FromLittleEndian(le));
        }
    }

    #region Constants / Properties

    [TestMethod]
    public void Constants_ZeroOneMax_BasicProperties()
    {
        Assert.IsTrue(uint256.Zero.IsZero);
        Assert.IsFalse(uint256.One.IsZero);
        Assert.IsTrue(uint256.One.IsOne);
        Assert.IsFalse(uint256.Zero.IsOne);

        Assert.AreEqual(uint256.MaxValue, uint256.MaxValue);
        Assert.IsFalse(uint256.MaxValue.IsZero);

        Assert.IsTrue(uint256.Zero < uint256.One);
        Assert.IsTrue(uint256.MaxValue > uint256.One);
    }

    [TestMethod]
    public void Properties_FitsAndLowHigh()
    {
        var a = new uint256(123UL);
        Assert.IsTrue(a.FitsInUlong);
        Assert.IsTrue(a.FitsInUInt128);
        Assert.AreEqual(123UL, a.Low64);

        var b = new uint256(0, 1, 0, 0); // 2^64
        Assert.IsFalse(b.FitsInUlong);
        Assert.IsTrue(b.FitsInUInt128);

        var c = new uint256(0, 0, 1, 0); // 2^128
        Assert.IsFalse(c.FitsInUlong);
        Assert.IsFalse(c.FitsInUInt128);

        // Verify Low128/High128 via bit composition (no ctor assumptions).
        UInt128 expectedLow = ((UInt128)1 << 64) | 0;
        Assert.AreEqual(expectedLow, b.Low128);
        Assert.AreEqual((UInt128)0, b.High128);

        UInt128 expectedHigh = ((UInt128)0 << 64) | 1;
        Assert.AreEqual((UInt128)0, c.Low128);
        Assert.AreEqual(expectedHigh, c.High128);
    }

    [TestMethod]
    public void Constructor_FromUInt128Halves_RoundTrip()
    {
        UInt128 low = ((UInt128)0x0123_4567_89AB_CDEFUL << 64) | 0x0FED_CBA9_8765_4321UL;
        UInt128 high = ((UInt128)0x1122_3344_5566_7788UL << 64) | 0x99AA_BBCC_DDEE_FF00UL;

        var v = new uint256(low, high);

        Span<byte> be = stackalloc byte[32];
        v.WriteBigEndian(be);

        // Re-create from the bytes and compare.
        var roundTrip = new uint256(be);
        Assert.AreEqual(v, roundTrip);
    }

    #endregion

    #region Byte conversions

    [TestMethod]
    public void Ctor_FromBigEndianBytes_Empty_IsZero()
    {
        var v = new uint256(ReadOnlySpan<byte>.Empty);
        Assert.AreEqual(uint256.Zero, v);
    }

    [TestMethod]
    public void Ctor_FromBigEndianBytes_LeftPadsZeros()
    {
        // 0x01 => 1
        var v1 = new uint256(new byte[] { 0x01 });
        Assert.AreEqual(new BigInteger(1), v1.ToBigInteger());

        // 0x01 0x00 => 256
        var v2 = new uint256(new byte[] { 0x01, 0x00 });
        Assert.AreEqual(new BigInteger(256), v2.ToBigInteger());
    }

    [TestMethod]
    public void FromLittleEndian_RightPadsZeros()
    {
        // {0x01} => 1
        var v1 = uint256.FromLittleEndian(new byte[] { 0x01 });
        Assert.AreEqual(new BigInteger(1), v1.ToBigInteger());

        // {0x00, 0x01} => 256
        var v2 = uint256.FromLittleEndian(new byte[] { 0x00, 0x01 });
        Assert.AreEqual(new BigInteger(256), v2.ToBigInteger());
    }

    [TestMethod]
    public void BigEndianCtor_TooLong_Throws()
    {
        var bytes = new byte[33];
        Assert.Throws<ArgumentException>(() => _ = new uint256(bytes));
    }

    [TestMethod]
    public void FromLittleEndian_TooLong_Throws()
    {
        var bytes = new byte[33];
        Assert.Throws<ArgumentException>(() => _ = uint256.FromLittleEndian(bytes));
    }

    [TestMethod]
    public void WriteBigEndian_And_BigEndianCtor_RoundTrip()
    {
        var original = new uint256(
            0x0123456789ABCDEFUL,
            0x0FEDCBA987654321UL,
            0x1122334455667788UL,
            0x99AABBCCDDEEFF00UL);

        Span<byte> be = stackalloc byte[32];
        original.WriteBigEndian(be);

        var roundTripped = new uint256(be);
        Assert.AreEqual(original, roundTripped);
    }

    [TestMethod]
    public void WriteLittleEndian_And_FromLittleEndian_RoundTrip()
    {
        var original = new uint256(
            0x0123456789ABCDEFUL,
            0x0FEDCBA987654321UL,
            0x1122334455667788UL,
            0x99AABBCCDDEEFF00UL);

        Span<byte> le = stackalloc byte[32];
        original.WriteLittleEndian(le);

        var roundTripped = uint256.FromLittleEndian(le);
        Assert.AreEqual(original, roundTripped);
    }

    [TestMethod]
    public void WriteBigEndian_KnownEncoding_ForOne()
    {
        Span<byte> be = stackalloc byte[32];
        uint256.One.WriteBigEndian(be);

        Assert.IsTrue(be.Slice(0, 31).ToArray().All(b => b == 0x00));
        Assert.AreEqual(0x01, be[31]);
    }

    [TestMethod]
    public void WriteLittleEndian_KnownEncoding_ForOne()
    {
        Span<byte> le = stackalloc byte[32];
        uint256.One.WriteLittleEndian(le);

        Assert.AreEqual(0x01, le[0]);
        Assert.IsTrue(le.Slice(1).ToArray().All(b => b == 0x00));
    }


    [TestMethod]
    public void ToBigEndianBytes_And_ToLittleEndianBytes_Are32Bytes()
    {
        Assert.HasCount(32, uint256.Zero.ToBigEndianBytes());
        Assert.HasCount(32, uint256.Zero.ToLittleEndianBytes());
        Assert.HasCount(32, uint256.MaxValue.ToBigEndianBytes());
        Assert.HasCount(32, uint256.MaxValue.ToLittleEndianBytes());
    }

    [TestMethod]
    public void GetByteCount_And_ToMinimalBigEndianBytes()
    {
        Assert.AreEqual(0, uint256.Zero.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 0 }, uint256.Zero.ToMinimalBigEndianBytes());

        Assert.AreEqual(1, uint256.One.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 0x01 }, uint256.One.ToMinimalBigEndianBytes());

        var v = uint256.Parse("0x0100".AsSpan(), CultureInfo.InvariantCulture); // 256
        Assert.AreEqual(2, v.GetByteCount());
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00 }, v.ToMinimalBigEndianBytes());

        Assert.AreEqual(32, uint256.MaxValue.GetByteCount());
        Assert.HasCount(32, uint256.MaxValue.ToMinimalBigEndianBytes());
    }

    #endregion

    #region Equality / Hashing / Comparison

    [TestMethod]
    public void Equals_OperatorAndObjectEquality()
    {
        var a = new uint256(123UL);
        var b = new uint256(123UL);
        var c = new uint256(124UL);

        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);

        Assert.IsFalse(a == c);
        Assert.IsTrue(a != c);

        Assert.IsTrue(a.Equals((object)b));
        Assert.IsFalse(a.Equals((object)c));
        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualValuesHaveSameHash()
    {
        var a = new uint256(42UL);
        var b = new uint256(42UL);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void CompareTo_UnsignedOrdering_Works()
    {
        Assert.IsTrue(uint256.Zero < uint256.One);
        Assert.IsTrue(uint256.One < uint256.MaxValue);
        Assert.IsTrue(uint256.MaxValue > uint256.One);

        var a = new uint256(0, 0, 0, 1); // 2^192
        var b = new uint256(ulong.MaxValue, 0, 0, 0);
        Assert.IsTrue(a > b);
    }

    [TestMethod]
    public void CompareTo_ObjectOverload()
    {
        var v = new uint256(1UL);

        Assert.AreEqual(1, v.CompareTo(null));
        Assert.AreEqual(0, v.CompareTo((object)new uint256(1UL)));

        Assert.Throws<ArgumentException>(() => v.CompareTo("not a uint256"));
    }

    #endregion

    #region Arithmetic

    [TestMethod]
    public void AddUnchecked_Wraps_Mod2Pow256()
    {
        var wrapped = uint256.AddUnchecked(uint256.MaxValue, uint256.One);
        Assert.AreEqual(uint256.Zero, wrapped);
    }

    [TestMethod]
    public void SubtractUnchecked_Wraps_Mod2Pow256()
    {
        var wrapped = uint256.SubtractUnchecked(uint256.Zero, uint256.One);
        Assert.AreEqual(uint256.MaxValue, wrapped);
    }

    [TestMethod]
    public void Addition_Checked_Overflow_Throws()
    {
        Assert.Throws<OverflowException>(() => _ = uint256.MaxValue + uint256.One);
    }

    [TestMethod]
    public void Subtraction_Checked_Underflow_Throws()
    {
        Assert.Throws<OverflowException>(() => _ = uint256.Zero - uint256.One);
    }

    [TestMethod]
    public void Multiply_Checked_Overflow_Throws()
    {
        Assert.Throws<OverflowException>(() => _ = uint256.MaxValue * new uint256(2UL));
    }

    [TestMethod]
    public void MultiplyUnchecked_Wraps_Mod2Pow256()
    {
        // (2^256-1) * 2 mod 2^256 = 2^256 - 2
        var v = uint256.MultiplyUnchecked(uint256.MaxValue, new uint256(2UL));
        Assert.AreEqual(TwoPow256 - 2, v.ToBigInteger());
    }

    [TestMethod]
    public void DivideModulo_64BitFastPath_AgreeWithBigInteger()
    {
        var dividend = uint256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        ulong divisor = 1_000_000_007UL;

        var q = dividend / new uint256(divisor);
        var r = dividend % new uint256(divisor);

        BigInteger bi = dividend.ToBigInteger();
        Assert.AreEqual(bi / divisor, q.ToBigInteger());
        Assert.AreEqual(bi % divisor, r.ToBigInteger());
    }

    [TestMethod]
    public void DivideModulo_GeneralDivisor_ColdPath_AgreeWithBigInteger()
    {
        var dividend = uint256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        var divisor = uint256.Parse("0x100000000000000000000000000000000".AsSpan(), CultureInfo.InvariantCulture); // 2^128

        var q = dividend / divisor;
        var r = dividend % divisor;

        BigInteger biA = dividend.ToBigInteger();
        BigInteger biB = divisor.ToBigInteger();

        Assert.AreEqual(biA / biB, q.ToBigInteger());
        Assert.AreEqual(biA % biB, r.ToBigInteger());
    }

    [TestMethod]
    public void DivRem_Static_AgreeWithBigInteger()
    {
        var a = uint256.Parse("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef".AsSpan(), CultureInfo.InvariantCulture);
        var b = uint256.Parse("0x1000000000000000000000000000000".AsSpan(), CultureInfo.InvariantCulture); // 2^124

        var (q, r) = uint256.DivRem(a, b);

        BigInteger biA = a.ToBigInteger();
        BigInteger biB = b.ToBigInteger();

        Assert.AreEqual(biA / biB, q.ToBigInteger());
        Assert.AreEqual(biA % biB, r.ToBigInteger());
    }

    [TestMethod]
    public void TryMultiply_FactorEdgeCases_AndOverflow()
    {
        var v = uint256.Parse("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF0".AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(v.TryMultiply(0, out var r0));
        Assert.AreEqual(uint256.Zero, r0);

        Assert.IsTrue(v.TryMultiply(1, out var r1));
        Assert.AreEqual(v, r1);

        Assert.IsFalse(uint256.MaxValue.TryMultiply(2, out var overflow));
        Assert.AreEqual(uint256.Zero, overflow);
    }

    [TestMethod]
    public void DivRem_U64_Instance_AgreeWithBigInteger()
    {
        var v = uint256.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        ulong d = 97UL;

        var q = v.DivRem(d, out ulong rem);

        BigInteger bi = v.ToBigInteger();
        Assert.AreEqual(bi / d, q.ToBigInteger());
        Assert.AreEqual((ulong)(bi % d), rem);
    }

    [TestMethod]
    public void DivideByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => _ = uint256.One / uint256.Zero);
        Assert.Throws<DivideByZeroException>(() => _ = uint256.One % uint256.Zero);
        Assert.Throws<DivideByZeroException>(() => _ = uint256.DivRem(uint256.One, uint256.Zero));
        Assert.Throws<DivideByZeroException>(() => _ = uint256.One.DivRem(0, out _));
    }

    [TestMethod]
    public void IncrementDecrement_Checked()
    {
        var x = uint256.Zero;
        x++;
        Assert.AreEqual(uint256.One, x);

        x--;
        Assert.AreEqual(uint256.Zero, x);

        Assert.Throws<OverflowException>(() =>
        {
            var y = uint256.Zero;
            y--;
        });

        Assert.Throws<OverflowException>(() =>
        {
            var y = uint256.MaxValue;
            y++;
        });
    }

    [TestMethod]
    public void UnaryNegation_Wraps_Mod2Pow256()
    {
        // -(1) mod 2^256 == 2^256 - 1
        var neg1 = -uint256.One;
        Assert.AreEqual(uint256.MaxValue, neg1);

        var neg0 = -uint256.Zero;
        Assert.AreEqual(uint256.Zero, neg0);
    }

    [TestMethod]
    public void Pow10U64_ValidAndInvalid()
    {
        Assert.AreEqual(1UL, uint256.Pow10U64(0));
        Assert.AreEqual(10UL, uint256.Pow10U64(1));
        Assert.AreEqual(1_000_000_000_000_000_000UL, uint256.Pow10U64(18));

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.Pow10U64(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.Pow10U64(20));
    }

    #endregion

    #region Bitwise / Shifts / Bit counts

    [TestMethod]
    public void BitwiseOps_BasicIdentities()
    {
        var a = new uint256(0x55UL);
        var b = new uint256(0x0FUL);

        Assert.AreEqual(new uint256(0x05UL), (a & b));
        Assert.AreEqual(new uint256(0x5FUL), (a | b));
        Assert.AreEqual(new uint256(0x5AUL), (a ^ b));
        Assert.AreEqual(uint256.MaxValue, (~uint256.Zero));
    }

    [TestMethod]
    public void Shifts_BasicAndEdgeBehaviour()
    {
        Assert.AreEqual(new uint256(2UL), (uint256.One << 1));
        Assert.AreEqual(uint256.Zero, (uint256.One >> 1));

        // shift >= 256 -> zero
        Assert.AreEqual(uint256.Zero, (uint256.One << 256));
        Assert.AreEqual(uint256.Zero, (uint256.One >> 256));

        // negative shift throws
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.One << -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = uint256.One >> -1);

        // high-bit behaviour (unsigned)
        var topBit = uint256.One << 255;
        Assert.AreEqual(uint256.One, (topBit >> 255));
    }

    [TestMethod]
    public void LeadingTrailingZeroCount_And_PopCount()
    {
        Assert.AreEqual(256, uint256.Zero.LeadingZeroCount());
        Assert.AreEqual(256, uint256.Zero.TrailingZeroCount());
        Assert.AreEqual(0, uint256.Zero.PopCount());

        Assert.AreEqual(255, uint256.One.LeadingZeroCount());
        Assert.AreEqual(0, uint256.One.TrailingZeroCount());
        Assert.AreEqual(1, uint256.One.PopCount());

        var topBit = uint256.One << 255;
        Assert.AreEqual(0, topBit.LeadingZeroCount());
        Assert.AreEqual(255, topBit.TrailingZeroCount());
        Assert.AreEqual(1, topBit.PopCount());

        var twoBits = uint256.One | (uint256.One << 200);
        Assert.AreEqual(2, twoBits.PopCount());
    }

    #endregion

    #region Conversions

    [TestMethod]
    public void ImplicitFromIntAndLong_NegativeThrows()
    {
        Assert.Throws<OverflowException>(() =>
        {
            uint256 _ = (int)-1;
        });

        Assert.Throws<OverflowException>(() =>
        {
            uint256 _ = (long)-1;
        });
    }

    [TestMethod]
    public void ExplicitToUlong_And_UInt128_RangeChecks()
    {
        var small = new uint256(123UL);
        Assert.AreEqual(123UL, (ulong)small);

        var bigForUlong = new uint256(0, 1, 0, 0); // 2^64
        Assert.Throws<OverflowException>(() => _ = (ulong)bigForUlong);

        var bigForUInt128 = new uint256(0, 0, 1, 0); // 2^128
        Assert.Throws<OverflowException>(() => _ = (UInt128)bigForUInt128);

        var fitsUInt128 = new uint256(((UInt128)123 << 64) | 456, 0);
        Assert.AreEqual(((UInt128)123 << 64) | 456, (UInt128)fitsUInt128);
    }

    [TestMethod]
    public void BigInteger_RoundTrip_And_RangeChecks()
    {
        var values = new[]
        {
                uint256.Zero,
                uint256.One,
                uint256.MaxValue,
                uint256.Parse("0x1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture),
                uint256.Parse("0x1000000000000000000000000000000000000000000000000000000000000000".AsSpan(), CultureInfo.InvariantCulture), // 2^252
            };

        foreach (var v in values)
        {
            BigInteger bi = v.ToBigInteger();
            var rt = (uint256)bi;
            Assert.AreEqual(v, rt);
        }

        Assert.Throws<OverflowException>(() => _ = (uint256)(-BigInteger.One));
        Assert.Throws<OverflowException>(() => _ = (uint256)(TwoPow256)); // 2^256 (out of range)
    }

    #endregion

    #region Parsing (char / UTF-8)

    [TestMethod]
    public void TryParse_Char_HexAndDecimal_Boundaries()
    {
        // Max in hex (64 digits)
        Assert.IsTrue(uint256.TryParse(("0x" + HexMaxValue64Lower).AsSpan(), CultureInfo.InvariantCulture, out var max1));
        Assert.AreEqual(uint256.MaxValue, max1);

        Assert.IsTrue(uint256.TryParse(("0X" + HexMaxValue64Upper).AsSpan(), CultureInfo.InvariantCulture, out var max2));
        Assert.AreEqual(uint256.MaxValue, max2);

        // Max in decimal
        string maxDec = (TwoPow256 - BigInteger.One).ToString(CultureInfo.InvariantCulture);
        Assert.IsTrue(uint256.TryParse(maxDec.AsSpan(), CultureInfo.InvariantCulture, out var maxDecParsed));
        Assert.AreEqual(uint256.MaxValue, maxDecParsed);

        // Out of range decimal: 2^256
        string tooBig = TwoPow256.ToString(CultureInfo.InvariantCulture);
        Assert.IsFalse(uint256.TryParse(tooBig.AsSpan(), CultureInfo.InvariantCulture, out var fail));
        Assert.AreEqual(uint256.Zero, fail);
    }

    [TestMethod]
    public void TryParse_Char_Accepts_0x_AsZero_And_TrimsWhitespace()
    {
        Assert.IsTrue(uint256.TryParse("0x".AsSpan(), CultureInfo.InvariantCulture, out var r));
        Assert.AreEqual(uint256.Zero, r);

        Assert.IsTrue(uint256.TryParse("   0x1   ".AsSpan(), CultureInfo.InvariantCulture, out var r2));
        Assert.AreEqual(uint256.One, r2);

        Assert.IsTrue(uint256.TryParse("  123  ".AsSpan(), CultureInfo.InvariantCulture, out var r3));
        Assert.AreEqual(new BigInteger(123), r3.ToBigInteger());
    }

    [TestMethod]
    public void TryParse_Char_InvalidInputs_ReturnFalse()
    {
        Assert.IsFalse(uint256.TryParse("".AsSpan(), CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(uint256.TryParse("   ".AsSpan(), CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(uint256.TryParse("0xZZ".AsSpan(), CultureInfo.InvariantCulture, out _));
        Assert.IsFalse(uint256.TryParse("-1".AsSpan(), CultureInfo.InvariantCulture, out _)); // unsigned
        Assert.IsFalse(uint256.TryParse(("0x" + new string('1', 65)).AsSpan(), CultureInfo.InvariantCulture, out _)); // >64 hex digits
    }

    [TestMethod]
    public void TryParse_String_Null_ReturnsFalse()
    {
        Assert.IsFalse(uint256.TryParse((string?)null, out var r));
        Assert.AreEqual(uint256.Zero, r);

        Assert.IsFalse(uint256.TryParse((string?)null, CultureInfo.InvariantCulture, out var r2));
        Assert.AreEqual(uint256.Zero, r2);
    }

    [TestMethod]
    public void TryParse_Utf8_Unquoted_And_Quoted()
    {
        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("123"), out var a));
        Assert.AreEqual(new BigInteger(123), a.ToBigInteger());

        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("\"0x1\""), out var b));
        Assert.AreEqual(uint256.One, b);

        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("  \"0x\"  "), out var c));
        Assert.AreEqual(uint256.Zero, c);
    }

    [TestMethod]
    public void TryParse_Utf8_Invalid_ReturnsFalse()
    {
        Assert.IsFalse(uint256.TryParse(Encoding.UTF8.GetBytes(""), out _));
        Assert.IsFalse(uint256.TryParse(Encoding.UTF8.GetBytes("\"\""), out _));
        Assert.IsFalse(uint256.TryParse(Encoding.UTF8.GetBytes("0xZZ"), out _));
        Assert.IsFalse(uint256.TryParse(Encoding.UTF8.GetBytes("-1"), out _));
        Assert.IsFalse(uint256.TryParse(Encoding.UTF8.GetBytes("0x" + new string('f', 65)), out _));
    }

    [TestMethod]
    public void Parse_ThrowsOnInvalid()
    {
        Assert.Throws<FormatException>(() => _ = uint256.Parse("not-a-number", CultureInfo.InvariantCulture));
        Assert.Throws<FormatException>(() => _ = uint256.Parse(Encoding.UTF8.GetBytes("not-a-number")));
    }

    [TestMethod]
    public void ParseDecimal_And_TryParseDecimal()
    {
        var v = uint256.ParseDecimal("  12345  ".AsSpan());
        Assert.AreEqual(new BigInteger(12345), v.ToBigInteger());

        Assert.IsTrue(uint256.TryParseDecimal("  12345  ".AsSpan(), out var t));
        Assert.AreEqual(v, t);

        Assert.IsFalse(uint256.TryParseDecimal("  -1  ".AsSpan(), out var f));
        Assert.AreEqual(uint256.Zero, f);
    }

    #endregion

    #region Formatting

    [TestMethod]
    public void ToString_Default_Is0xPrefixedHex()
    {
        Assert.AreEqual("0x0", uint256.Zero.ToString());
        Assert.AreEqual("0x1", uint256.One.ToString());
    }

    [TestMethod]
    public void ToString_Hex_NoPrefix_Minimal()
    {
        Assert.AreEqual("0", uint256.Zero.ToString("x", CultureInfo.InvariantCulture));
        Assert.AreEqual("1", uint256.One.ToString("x", CultureInfo.InvariantCulture));

        var twoPow64 = uint256.Parse("0x10000000000000000".AsSpan(), CultureInfo.InvariantCulture);
        Assert.AreEqual("10000000000000000", twoPow64.ToString("x", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_Hex_WithPrefix_And_MaxValue()
    {
        Assert.AreEqual("0x" + HexMaxValue64Lower, uint256.MaxValue.ToString("0x", CultureInfo.InvariantCulture));
        Assert.AreEqual("0X" + HexMaxValue64Upper, uint256.MaxValue.ToString("0X", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_Decimal_UsesBigIntegerColdPath()
    {
        var v = uint256.Parse("0xDE0B6B3A7640000".AsSpan(), CultureInfo.InvariantCulture); // 1e18
        Assert.AreEqual(v.ToBigInteger().ToString(CultureInfo.InvariantCulture), v.ToString("D", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_CharDestination_TooSmall_ReturnsFalse()
    {
        Span<char> small = stackalloc char[1];
        Assert.IsFalse(uint256.MaxValue.TryFormat(small, out _, "0x".AsSpan(), CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_CharDestination_Succeeds_ForCommonFormats()
    {
        Span<char> dst = stackalloc char[80];

        Assert.IsTrue(uint256.One.TryFormat(dst, out int w, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("0x1", new string(dst.Slice(0, w)));

        Assert.IsTrue(uint256.MaxValue.TryFormat(dst, out w, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("0x" + HexMaxValue64Lower, new string(dst.Slice(0, w)));

        Assert.IsTrue(uint256.One.TryFormat(dst, out w, "D".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("1", new string(dst.Slice(0, w)));
    }

    [TestMethod]
    public void TryFormat_Utf8Destination_Succeeds_AndIsAscii()
    {
        Span<byte> utf8 = stackalloc byte[80];

        Assert.IsTrue(uint256.One.TryFormat(utf8, out int bw, "0x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("0x1", Encoding.ASCII.GetString(utf8.Slice(0, bw)));

        Assert.IsTrue(uint256.MaxValue.TryFormat(utf8, out bw, "x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(HexMaxValue64Lower, Encoding.ASCII.GetString(utf8.Slice(0, bw)));
    }

    [TestMethod]
    public void TryFormat_Utf8Destination_TooSmall_ReturnsFalse()
    {
        Span<byte> small = stackalloc byte[2];
        Assert.IsFalse(uint256.MaxValue.TryFormat(small, out _, "0x".AsSpan(), CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_InvalidFormat_Throws()
    {
        Assert.Throws<FormatException>(() => _ = uint256.One.ToString("NOPE", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToFullHexString_IsFixedWidth64_NoPrefix()
    {
        string s0 = uint256.Zero.ToFullHexString();
        Assert.AreEqual(64, s0.Length);
        Assert.IsTrue(s0.All(c => c == '0'));

        string s1 = uint256.One.ToFullHexString();
        Assert.AreEqual(64, s1.Length);
        Assert.IsTrue(s1.StartsWith(new string('0', 63), StringComparison.Ordinal));
        Assert.IsTrue(s1.EndsWith("1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToDecimalString_MatchesBigInteger()
    {
        var v = uint256.Parse("0x1234567890abcdef1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        Assert.AreEqual(v.ToBigInteger().ToString(CultureInfo.InvariantCulture), v.ToDecimalString());
    }

    #endregion

    #region ABI / Chain helpers

    [TestMethod]
    public void AbiEncoding_RoundTrip()
    {
        var v = uint256.Parse("0x1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);

        byte[] abi = v.ToAbiEncoded();
        Assert.HasCount(32, abi);

        var roundTrip = uint256.FromAbiEncoded(abi);
        Assert.AreEqual(v, roundTrip);
    }

    [TestMethod]
    public void FromAbiEncoded_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => _ = uint256.FromAbiEncoded(new byte[31]));
    }

    [TestMethod]
    public void EvmAndSolana_Constants_AreCorrect()
    {
        Assert.AreEqual(BigInteger.Pow(10, 18), uint256.Evm.WeiPerEther.ToBigInteger());
        Assert.AreEqual(new BigInteger(1_000_000_000UL), uint256.Evm.WeiPerGwei.ToBigInteger());
        Assert.AreEqual(new BigInteger(1_000_000_000UL), uint256.Solana.LamportsPerSol.ToBigInteger());

        Assert.AreEqual("0x" + "de0b6b3a7640000", uint256.Evm.WeiPerEther.ToEvmHex());
    }

    #endregion
}
