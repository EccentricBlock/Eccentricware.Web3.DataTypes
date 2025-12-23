using EccentricWare.Web3.DataTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace EccentricWare.Web3.DataTypes.Tests
{
    [TestClass]
    public sealed class Int256Tests
    {
        private static readonly BigInteger TwoPow255 = BigInteger.One << 255;
        private static readonly string HexMinValue64 = "8" + new string('0', 63);
        private static readonly string HexMaxValue64 = "7" + new string('f', 63);
        private static readonly string HexMinusOne64 = new string('f', 64);
        private static readonly string HexMinusOne64Upper = new string('F', 64);

        #region Constants / Properties

        [TestMethod]
        public void Constants_ZeroOneMinusOne_MinMax_BasicProperties()
        {
            Assert.IsTrue(int256.Zero.IsZero);
            Assert.IsFalse(int256.Zero.IsNegative);
            Assert.AreEqual(0, int256.Zero.Sign);

            Assert.IsFalse(int256.One.IsZero);
            Assert.IsFalse(int256.One.IsNegative);
            Assert.AreEqual(1, int256.One.Sign);

            Assert.IsFalse(int256.MinusOne.IsZero);
            Assert.IsTrue(int256.MinusOne.IsNegative);
            Assert.AreEqual(-1, int256.MinusOne.Sign);

            Assert.IsTrue(int256.MinValue.IsNegative);
            Assert.IsFalse(int256.MinValue.IsZero);

            Assert.IsFalse(int256.MaxValue.IsNegative);
            Assert.IsFalse(int256.MaxValue.IsZero);

            Assert.IsTrue(int256.MinValue < int256.Zero);
            Assert.IsTrue(int256.MaxValue > int256.Zero);
            Assert.IsTrue(int256.MinValue < int256.MaxValue);
        }

        #endregion

        #region Byte conversions

        [TestMethod]
        public void Ctor_FromBytes_Empty_IsZero()
        {
            var v = new int256(ReadOnlySpan<byte>.Empty);
            Assert.AreEqual(int256.Zero, v);
        }

        [TestMethod]
        public void Ctor_FromBytes_SignExtends_Positive()
        {
            var v = new int256(new byte[] { 0x7F }); // 127
            Assert.AreEqual(new BigInteger(127), v.ToBigInteger());

            var v2 = new int256(new byte[] { 0x00, 0x80 }); // 128
            Assert.AreEqual(new BigInteger(128), v2.ToBigInteger());
        }

        [TestMethod]
        public void Ctor_FromBytes_SignExtends_Negative()
        {
            var v = new int256(new byte[] { 0xFF }); // -1
            Assert.AreEqual(int256.MinusOne, v);

            var v2 = new int256(new byte[] { 0x80 }); // -128
            Assert.AreEqual(new BigInteger(-128), v2.ToBigInteger());

            var v3 = new int256(new byte[] { 0x80, 0x00 }); // -32768
            Assert.AreEqual(new BigInteger(-32768), v3.ToBigInteger());
        }

        [TestMethod]
        public void Ctor_FromBytes_TooLong_Throws()
        {
            var bytes = new byte[33];
            Assert.Throws<ArgumentException>(() => _ = new int256(bytes));
        }

        [TestMethod]
        public void WriteBigEndian_And_CtorFrom32Bytes_RoundTrips()
        {
            var original = new int256(
                0x0123456789ABCDEFUL,
                0x0FEDCBA987654321UL,
                0x1122334455667788UL,
                0x99AABBCCDDEEFF00UL);

            Span<byte> be = stackalloc byte[32];
            original.WriteBigEndian(be);

            var roundTripped = new int256(be);
            Assert.AreEqual(original, roundTripped);
        }

        [TestMethod]
        public void WriteLittleEndian_RoundTrips_ViaBigInteger()
        {
            var original = (int256)new BigInteger(-1234567890123456789L);

            Span<byte> le = stackalloc byte[32];
            original.WriteLittleEndian(le);

            var bi = new BigInteger(le, isUnsigned: false, isBigEndian: false);
            Assert.AreEqual(original.ToBigInteger(), bi);
        }

        [TestMethod]
        public void WriteBigEndian_KnownEncoding_ForOne_And_MinusOne()
        {
            Span<byte> be = stackalloc byte[32];

            int256.One.WriteBigEndian(be);
            Assert.IsTrue(be.Slice(0, 31).ToArray().All(b => b == 0x00));
            Assert.AreEqual(0x01, be[31]);

            int256.MinusOne.WriteBigEndian(be);
            Assert.IsTrue(be.ToArray().All(b => b == 0xFF));
        }

        [TestMethod]
        public void WriteLittleEndian_KnownEncoding_ForOne()
        {
            Span<byte> le = stackalloc byte[32];

            int256.One.WriteLittleEndian(le);
            Assert.AreEqual(0x01, le[0]);
            Assert.IsTrue(le.Slice(1).ToArray().All(b => b == 0x00));
        }

        [TestMethod]
        public void WriteBigEndian_DestinationTooSmall_Throws()
        {
            Assert.Throws<ArgumentException>(() => int256.Zero.WriteBigEndian(stackalloc byte[31]));
        }

        [TestMethod]
        public void WriteLittleEndian_DestinationTooSmall_Throws()
        {
            Assert.Throws<ArgumentException>(() => int256.Zero.WriteLittleEndian(stackalloc byte[0]));
        }

        [TestMethod]
        public void ToBigEndianBytes_And_ToLittleEndianBytes_Are32Bytes()
        {
            Assert.HasCount(32, int256.Zero.ToBigEndianBytes());
            Assert.HasCount(32, int256.Zero.ToLittleEndianBytes());
            Assert.HasCount(32, int256.MinusOne.ToBigEndianBytes());
            Assert.HasCount(32, int256.MinusOne.ToLittleEndianBytes());
        }

        #endregion

        #region Equality / Hashing / Comparison

        [TestMethod]
        public void Equals_OperatorAndObjectEquality()
        {
            var a = new int256(123);
            var b = new int256(123);
            var c = new int256(-123);

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
            var a = new int256(42);
            var b = new int256(42);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void CompareTo_SignedOrdering_WorksAcrossNegativesAndPositives()
        {
            Assert.IsTrue(new int256(-3) < new int256(-2));
            Assert.IsTrue(new int256(-2) < new int256(-1));
            Assert.IsTrue(new int256(-1) < int256.Zero);
            Assert.IsTrue(int256.Zero < int256.One);
            Assert.IsTrue(int256.One < new int256(2));
        }

        [TestMethod]
        public void CompareTo_ObjectOverload()
        {
            var v = new int256(1);

            Assert.AreEqual(1, v.CompareTo(null));
            Assert.AreEqual(0, v.CompareTo((object)new int256(1)));

            Assert.Throws<ArgumentException>(() => v.CompareTo("not an int256"));
        }

        #endregion

        #region Arithmetic

        [TestMethod]
        public void AddUnchecked_Wraps_Mod2Pow256()
        {
            var wrapped = int256.AddUnchecked(int256.MaxValue, int256.One);
            Assert.AreEqual(int256.MinValue, wrapped);
        }

        [TestMethod]
        public void SubtractUnchecked_Wraps_Mod2Pow256()
        {
            var wrapped = int256.SubtractUnchecked(int256.MinValue, int256.One);
            Assert.AreEqual(int256.MaxValue, wrapped);
        }

        [TestMethod]
        public void NegateUnchecked_MinValue_StaysMinValue()
        {
            var wrapped = int256.NegateUnchecked(int256.MinValue);
            Assert.AreEqual(int256.MinValue, wrapped);
        }

        [TestMethod]
        public void Addition_Checked_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => _ = int256.MaxValue + int256.One);
            Assert.Throws<OverflowException>(() => _ = int256.MinValue + int256.MinusOne);
        }

        [TestMethod]
        public void Subtraction_Checked_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => _ = int256.MaxValue - int256.MinusOne);
            Assert.Throws<OverflowException>(() => _ = int256.MinValue - int256.One);
        }

        [TestMethod]
        public void Negation_Checked_MinValue_Throws()
        {
            Assert.Throws<OverflowException>(() => _ = -int256.MinValue);
        }

        [TestMethod]
        public void BasicArithmetic_SmallValues_AgreeWithBigInteger()
        {
            var a = new int256(123456);
            var b = new int256(-789);

            Assert.AreEqual(((BigInteger)a + (BigInteger)b), (a + b).ToBigInteger());
            Assert.AreEqual(((BigInteger)a - (BigInteger)b), (a - b).ToBigInteger());
            Assert.AreEqual((-(BigInteger)a), (-a).ToBigInteger());
        }

        [TestMethod]
        public void MultiplyDivideModulo_ColdPath_AgreeWithBigInteger_ForSmallValues()
        {
            var a = (int256)new BigInteger(123456789);
            var b = (int256)new BigInteger(-1000);

            Assert.AreEqual(((BigInteger)a * (BigInteger)b), (a * b).ToBigInteger());
            Assert.AreEqual(((BigInteger)a / (BigInteger)b), (a / b).ToBigInteger());
            Assert.AreEqual(((BigInteger)a % (BigInteger)b), (a % b).ToBigInteger());
        }

        [TestMethod]
        public void Multiply_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => _ = int256.MaxValue * (int256)new BigInteger(2));
            Assert.Throws<OverflowException>(() => _ = int256.MinValue * (int256)new BigInteger(2));
        }

        [TestMethod]
        public void DivideByZero_Throws()
        {
            Assert.Throws<DivideByZeroException>(() => _ = int256.One / int256.Zero);
            Assert.Throws<DivideByZeroException>(() => _ = int256.One % int256.Zero);
        }

        #endregion

        #region Bitwise and Shifts

        [TestMethod]
        public void BitwiseOps_BasicIdentities()
        {
            var a = new int256(0x55);
            var b = new int256(0x0F);

            Assert.AreEqual(new int256(0x05), (a & b));
            Assert.AreEqual(new int256(0x5F), (a | b));
            Assert.AreEqual(new int256(0x5A), (a ^ b));
            Assert.AreEqual((int256)new BigInteger(~0x55), (~a).ToBigInteger());
        }

        [TestMethod]
        public void ShiftLeft_Basic()
        {
            Assert.AreEqual(new int256(2), (new int256(1) << 1));
            Assert.AreEqual(new int256(0), (int256.Zero << 123));
        }

        [TestMethod]
        public void ShiftRight_Arithmetic_ForNegativeValues()
        {
            Assert.AreEqual(int256.MinusOne, (int256.MinusOne >> 1));
            Assert.AreEqual(int256.MinusOne, (int256.MinusOne >> 255));
            Assert.AreEqual(int256.MinusOne, (int256.MinValue >> 255));
        }

        [TestMethod]
        public void ShiftRight_Arithmetic_ForPositiveValues()
        {
            Assert.AreEqual(int256.Zero, (int256.One >> 1));
            Assert.AreEqual(int256.One, (new int256(2) >> 1));
        }

        [TestMethod]
        public void Shift_Masking_Semantics_256BehavesLike0()
        {
            var v = (int256)new BigInteger(123456);

            Assert.AreEqual(v, (v << 256));
            Assert.AreEqual(v, (v >> 256));
            Assert.AreEqual(v, (v << 512));
            Assert.AreEqual(v, (v >> 512));
        }

        [TestMethod]
        public void ShiftLeft_ToSignBit_CreatesMinValue()
        {
            Assert.AreEqual(int256.MinValue, (int256.One << 255));
        }

        #endregion

        #region Conversions

        [TestMethod]
        public void ImplicitFromLong_SignExtends()
        {
            int256 pos = (int256)123L;
            int256 neg = (int256)(-123L);

            Assert.IsFalse(pos.IsNegative);
            Assert.IsTrue(neg.IsNegative);

            Assert.AreEqual(new BigInteger(123), pos.ToBigInteger());
            Assert.AreEqual(new BigInteger(-123), neg.ToBigInteger());
        }

        [TestMethod]
        public void ExplicitToLong_InRange_Works()
        {
            Assert.AreEqual(0L, (long)int256.Zero);
            Assert.AreEqual(1L, (long)int256.One);
            Assert.AreEqual(-1L, (long)int256.MinusOne);

            var v = (int256)(-42L);
            Assert.AreEqual(-42L, (long)v);
        }

        [TestMethod]
        public void ExplicitToLong_OutOfRange_Throws()
        {
            Assert.Throws<OverflowException>(() => _ = (long)int256.MinValue);
            Assert.Throws<OverflowException>(() => _ = (long)int256.MaxValue);

            var bi = (BigInteger.One << 80); // > long.MaxValue
            var v = (int256)bi;
            Assert.Throws<OverflowException>(() => _ = (long)v);
        }

        [TestMethod]
        public void ToBigInteger_RoundTrip_SmallAndLarge()
        {
            var values = new[]
            {
                int256.Zero,
                int256.One,
                int256.MinusOne,
                int256.MinValue,
                int256.MaxValue,
                (int256)(BigInteger.One << 200),
                (int256)(-(BigInteger.One << 200)),
            };

            foreach (var v in values)
            {
                BigInteger bi = v.ToBigInteger();
                var roundTrip = (int256)bi;
                Assert.AreEqual(v, roundTrip);
            }
        }

        [TestMethod]
        public void ExplicitFromBigInteger_RangeChecks()
        {
            BigInteger max = (BigInteger.One << 255) - BigInteger.One;
            BigInteger min = -(BigInteger.One << 255);

            Assert.AreEqual(int256.MaxValue, (int256)max);
            Assert.AreEqual(int256.MinValue, (int256)min);

            Assert.Throws<OverflowException>(() => _ = (int256)(BigInteger.One << 255));
            Assert.Throws<OverflowException>(() => _ = (int256)(-(BigInteger.One << 255) - BigInteger.One));
        }

        #endregion

        #region Parsing (char)

        [TestMethod]
        public void Parse_Decimal_Boundaries()
        {
            string maxDec = (TwoPow255 - BigInteger.One).ToString(CultureInfo.InvariantCulture);
            string minDec = (-TwoPow255).ToString(CultureInfo.InvariantCulture);

            Assert.AreEqual(int256.MaxValue, int256.Parse(maxDec, CultureInfo.InvariantCulture));
            Assert.AreEqual(int256.MinValue, int256.Parse(minDec, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TryParse_Decimal_Rejects_OutOfRange()
        {
            string tooBigPos = TwoPow255.ToString(CultureInfo.InvariantCulture); // 2^255 (positive overflow)
            string tooBigNegMag = (TwoPow255 + BigInteger.One).ToString(CultureInfo.InvariantCulture); // 2^255+1

            Assert.IsFalse(int256.TryParse(tooBigPos.AsSpan(), CultureInfo.InvariantCulture, out var r1));
            Assert.AreEqual(int256.Zero, r1);

            Assert.IsFalse(int256.TryParse(("-" + tooBigNegMag).AsSpan(), CultureInfo.InvariantCulture, out var r2));
            Assert.AreEqual(int256.Zero, r2);
        }

        [TestMethod]
        public void TryParse_Allows_NegativeZero()
        {
            Assert.IsTrue(int256.TryParse("-0".AsSpan(), CultureInfo.InvariantCulture, out var r));
            Assert.AreEqual(int256.Zero, r);

            Assert.IsTrue(int256.TryParseDecimal("-0", out var r2));
            Assert.AreEqual(int256.Zero, r2);
        }

        [TestMethod]
        public void Parse_Hex_WithPrefix_MinMaxMinusOne_RawTwosComplement_64Digits()
        {
            Assert.AreEqual(int256.MinValue, int256.Parse(("0x" + HexMinValue64).AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual(int256.MaxValue, int256.Parse(("0x" + HexMaxValue64).AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual(int256.MinusOne, int256.Parse(("0x" + HexMinusOne64).AsSpan(), CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void Parse_Hex_NegativePrefix_TreatedAsMagnitudeThenNegated()
        {
            Assert.AreEqual(int256.MinusOne, int256.Parse("-0x01".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual(int256.MinValue, int256.Parse(("-0x" + HexMinValue64).AsSpan(), CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TryParse_Hex_NegativePrefix_Rejects_TooLargeMagnitude()
        {
            string s = "-0x" + HexMinusOne64; // magnitude = 2^256-1 (invalid)
            Assert.IsFalse(int256.TryParse(s.AsSpan(), CultureInfo.InvariantCulture, out var r));
            Assert.AreEqual(int256.Zero, r);
        }

        [TestMethod]
        public void TryParse_Hex_Accepts_0x_AsZero()
        {
            Assert.IsTrue(int256.TryParse("0x".AsSpan(), CultureInfo.InvariantCulture, out var r));
            Assert.AreEqual(int256.Zero, r);
        }

        [TestMethod]
        public void TryParse_String_Null_ReturnsFalse()
        {
            Assert.IsFalse(int256.TryParse((string?)null, CultureInfo.InvariantCulture, out var r));
            Assert.AreEqual(int256.Zero, r);
        }

        [TestMethod]
        public void TryParse_InvalidInputs_ReturnFalse()
        {
            Assert.IsFalse(int256.TryParse("".AsSpan(), CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(int256.TryParse("   ".AsSpan(), CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(int256.TryParse("not-a-number".AsSpan(), CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(int256.TryParse("0xZZ".AsSpan(), CultureInfo.InvariantCulture, out _));
        }

        [TestMethod]
        public void TryParseDecimal_WhitespaceAndPlusSign()
        {
            Assert.IsTrue(int256.TryParseDecimal("  +42  ", out var r));
            Assert.AreEqual(new BigInteger(42), r.ToBigInteger());

            Assert.IsTrue(int256.TryParseDecimal("  -42  ", out var r2));
            Assert.AreEqual(new BigInteger(-42), r2.ToBigInteger());
        }

        #endregion

        #region Parsing (UTF-8)

        [TestMethod]
        public void TryParse_Utf8_Unquoted_And_Quoted()
        {
            Assert.IsTrue(int256.TryParse(Encoding.UTF8.GetBytes("123"), out var a));
            Assert.AreEqual(new BigInteger(123), a.ToBigInteger());

            Assert.IsTrue(int256.TryParse(Encoding.UTF8.GetBytes("\"123\""), out var b));
            Assert.AreEqual(new BigInteger(123), b.ToBigInteger());

            Assert.IsTrue(int256.TryParse(Encoding.UTF8.GetBytes("  \"-0x01\"  "), out var c));
            Assert.AreEqual(new BigInteger(-1), c.ToBigInteger());
        }

        [TestMethod]
        public void TryParse_Utf8_OutOfRange_ReturnsFalse()
        {
            string s = TwoPow255.ToString(CultureInfo.InvariantCulture); // 2^255
            Assert.IsFalse(int256.TryParse(Encoding.UTF8.GetBytes(s), out _));
        }

        #endregion

        #region Formatting

        [TestMethod]
        public void ToString_Default_IsDecimalInvariant()
        {
            Assert.AreEqual("0", int256.Zero.ToString());
            Assert.AreEqual("1", int256.One.ToString());
            Assert.AreEqual("-1", int256.MinusOne.ToString());
        }

        [TestMethod]
        public void ToString_D_Format_UsesSignedDecimal()
        {
            var v = (int256)new BigInteger(-1234567890123456789L);
            Assert.AreEqual(v.ToBigInteger().ToString(CultureInfo.InvariantCulture), v.ToString("D", CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void ToString_Hex_Positive_Minimal_NoPrefix()
        {
            Assert.AreEqual("0", int256.Zero.ToString("x", CultureInfo.InvariantCulture));
            Assert.AreEqual("1", int256.One.ToString("x", CultureInfo.InvariantCulture));

            var twoPow64 = (int256)(BigInteger.One << 64);
            Assert.AreEqual("10000000000000000", twoPow64.ToString("x", CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void ToString_Hex_Negative_IsFullWidthTwosComplement()
        {
            Assert.AreEqual(HexMinusOne64, int256.MinusOne.ToString("x", CultureInfo.InvariantCulture));
            Assert.AreEqual(HexMinusOne64Upper, int256.MinusOne.ToString("X", CultureInfo.InvariantCulture));
            Assert.AreEqual(HexMinValue64, int256.MinValue.ToString("x", CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void ToString_Hex_WithPrefix()
        {
            Assert.AreEqual("0x0", int256.Zero.ToString("0x", CultureInfo.InvariantCulture));
            Assert.AreEqual("0x1", int256.One.ToString("0x", CultureInfo.InvariantCulture));

            Assert.AreEqual("0x" + HexMinusOne64, int256.MinusOne.ToString("0x", CultureInfo.InvariantCulture));
            Assert.AreEqual("0X" + HexMinusOne64Upper, int256.MinusOne.ToString("0X", CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TryFormat_CharDestination_TooSmall_ReturnsFalse()
        {
            Span<char> small = stackalloc char[1];
            Assert.IsFalse(int256.MinusOne.TryFormat(small, out _, "0x".AsSpan(), CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TryFormat_CharDestination_Succeeds_ForCommonFormats()
        {
            Span<char> dst = stackalloc char[80];

            Assert.IsTrue(int256.MinusOne.TryFormat(dst, out int written, "D".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual("-1", new string(dst.Slice(0, written)));

            Assert.IsTrue(int256.MinusOne.TryFormat(dst, out written, "x".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual(new string('f', 64), new string(dst.Slice(0, written)));

            Assert.IsTrue(int256.One.TryFormat(dst, out written, "0x".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual("0x1", new string(dst.Slice(0, written)));
        }

        [TestMethod]
        public void TryFormat_Utf8Destination_Succeeds_AndIsAscii()
        {
            Span<byte> utf8 = stackalloc byte[80];

            Assert.IsTrue(int256.One.TryFormat(utf8, out int bw, "0x".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual("0x1", Encoding.ASCII.GetString(utf8.Slice(0, bw)));

            Assert.IsTrue(int256.MinusOne.TryFormat(utf8, out bw, "x".AsSpan(), CultureInfo.InvariantCulture));
            Assert.AreEqual(new string('f', 64), Encoding.ASCII.GetString(utf8.Slice(0, bw)));
        }

        [TestMethod]
        public void TryFormat_Utf8Destination_TooSmall_ReturnsFalse()
        {
            Span<byte> small = stackalloc byte[2];
            Assert.IsFalse(int256.MinusOne.TryFormat(small, out _, "x".AsSpan(), CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void ToString_InvalidFormat_Throws()
        {
            Assert.Throws<FormatException>(() => _ = int256.One.ToString("NOPE", CultureInfo.InvariantCulture));
        }

        #endregion
    }
}
