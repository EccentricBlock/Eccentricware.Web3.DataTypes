using System.Numerics;
using EccentricWare.Web3.DataTypes;

namespace EccentricWare.Web3.DataTypes.Test;

[TestClass]
public sealed class HexBigIntegerTests
{
    #region Construction Tests

    [TestMethod]
    public void Constructor_Default_IsZero()
    {
        HexBigInteger value = default;
        Assert.IsTrue(value.IsZero);
        Assert.AreEqual(HexBigInteger.Zero, value);
    }

    [TestMethod]
    public void Constructor_FromBigInteger_CorrectValue()
    {
        var bigInt = BigInteger.Parse("12345678901234567890");
        HexBigInteger value = new(bigInt);
        Assert.AreEqual(bigInt, value.Value);
    }8

    [TestMethod]
    public void Constructor_FromLong_CorrectValue()
    {
        HexBigInteger value = new(0xDEADBEEFCAFEBABEL);
        Assert.AreEqual("0xdeadbeefcafebabe", value.ToString());
    }

    [TestMethod]
    public void Constructor_FromUlong_CorrectValue()
    {
        HexBigInteger value = new(0xDEADBEEFCAFEBABEUL);
        Assert.AreEqual("0xdeadbeefcafebabe", value.ToString());
    }

    [TestMethod]
    public void Constructor_FromBigEndianBytes_CorrectValue()
    {
        byte[] bytes = [0x01, 0x02, 0x03, 0x04];
        HexBigInteger value = new(bytes);
        Assert.AreEqual("0x1020304", value.ToString());
    }

    [TestMethod]
    public void Constructor_FromBigEndianBytes_LargeValue()
    {
        byte[] bytes = new byte[32];
        Array.Fill(bytes, (byte)0xFF);
        HexBigInteger value = new(bytes);
        Assert.AreEqual(new string('f', 64), value.ToHexString());
    }

    #endregion

    #region Equality Tests

    [TestMethod]
    public void Equals_SameValue_ReturnsTrue()
    {
        HexBigInteger a = new(123456789UL);
        HexBigInteger b = new(123456789UL);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        HexBigInteger a = new(123UL);
        HexBigInteger b = new(456UL);

        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Equals_LargeValues_CorrectComparison()
    {
        var largeValue = BigInteger.Parse("123456789012345678901234567890");
        HexBigInteger a = new(largeValue);
        HexBigInteger b = new(largeValue);
        HexBigInteger c = new(largeValue + 1);

        Assert.IsTrue(a == b);
        Assert.IsFalse(a == c);
    }

    [TestMethod]
    public void Equals_Object_CorrectBehavior()
    {
        HexBigInteger a = new(42);
        object b = new HexBigInteger(42);
        object c = "not a HexBigInteger";

        Assert.IsTrue(a.Equals(b));
        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualValues_SameHash()
    {
        HexBigInteger a = new(0xDEADBEEFCAFEBABEUL);
        HexBigInteger b = new(0xDEADBEEFCAFEBABEUL);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    #endregion

    #region Comparison Tests

    [TestMethod]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        HexBigInteger smaller = new(100);
        HexBigInteger larger = new(200);

        Assert.IsTrue(smaller.CompareTo(larger) < 0);
        Assert.IsTrue(smaller < larger);
        Assert.IsTrue(smaller <= larger);
    }

    [TestMethod]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        HexBigInteger smaller = new(100);
        HexBigInteger larger = new(200);

        Assert.IsTrue(larger.CompareTo(smaller) > 0);
        Assert.IsTrue(larger > smaller);
        Assert.IsTrue(larger >= smaller);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        HexBigInteger a = new(12345);
        HexBigInteger b = new(12345);

        Assert.AreEqual(0, a.CompareTo(b));
        Assert.IsTrue(a <= b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_LargeValues_CorrectComparison()
    {
        var large = BigInteger.Parse("999999999999999999999999999999999999999");
        HexBigInteger smaller = new(large);
        HexBigInteger larger = new(large + 1);

        Assert.IsTrue(smaller < larger);
    }

    [TestMethod]
    public void CompareTo_Object_CorrectBehavior()
    {
        HexBigInteger a = new(100);
        object b = new HexBigInteger(200);

        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(a.CompareTo(null) > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CompareTo_InvalidObject_Throws()
    {
        HexBigInteger a = new(100);
        a.CompareTo("invalid");
    }

    [TestMethod]
    public void Comparison_AllOperators_Work()
    {
        HexBigInteger a = new(10);
        HexBigInteger b = new(20);
        HexBigInteger c = new(10);

        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= b);
        Assert.IsTrue(b >= a);
        Assert.IsTrue(a <= c);
        Assert.IsTrue(a >= c);
    }

    #endregion

    #region Conversion Tests

    [TestMethod]
    public void ImplicitConversion_FromBigInteger_Works()
    {
        BigInteger bigInt = BigInteger.Parse("123456789");
        HexBigInteger value = bigInt;
        Assert.AreEqual(bigInt, value.Value);
    }

    [TestMethod]
    public void ImplicitConversion_FromLong_Works()
    {
        HexBigInteger value = 0xCAFEBABEL;
        Assert.AreEqual("0xcafebabe", value.ToString());
    }

    [TestMethod]
    public void ImplicitConversion_FromUlong_Works()
    {
        HexBigInteger value = 0xCAFEBABEUL;
        Assert.AreEqual("0xcafebabe", value.ToString());
    }

    [TestMethod]
    public void ImplicitConversion_ToBigInteger_Works()
    {
        HexBigInteger value = new(12345);
        BigInteger result = value;
        Assert.AreEqual(new BigInteger(12345), result);
    }

    [TestMethod]
    public void ExplicitConversion_ToLong_SmallValue_Works()
    {
        HexBigInteger value = new(12345L);
        long result = (long)value;
        Assert.AreEqual(12345L, result);
    }

    [TestMethod]
    public void ExplicitConversion_ToUlong_SmallValue_Works()
    {
        HexBigInteger value = new(12345UL);
        ulong result = (ulong)value;
        Assert.AreEqual(12345UL, result);
    }

    [TestMethod]
    [ExpectedException(typeof(OverflowException))]
    public void ExplicitConversion_ToLong_LargeValue_Throws()
    {
        var large = BigInteger.Parse("99999999999999999999999999999999999");
        HexBigInteger value = new(large);
        _ = (long)value;
    }

    #endregion

    #region Parse Tests

    [TestMethod]
    public void Parse_HexWithPrefix_CorrectValue()
    {
        HexBigInteger value = HexBigInteger.Parse("0x1234");
        Assert.AreEqual("0x1234", value.ToString());
    }

    [TestMethod]
    public void Parse_HexWithoutPrefix_CorrectValue()
    {
        HexBigInteger value = HexBigInteger.Parse("abcd");
        Assert.AreEqual("0xabcd", value.ToString());
    }

    [TestMethod]
    public void Parse_LargeHex_CorrectValue()
    {
        string hex = new string('f', 64);
        HexBigInteger value = HexBigInteger.Parse(hex);
        Assert.AreEqual("0x" + hex, value.ToString());
    }

    [TestMethod]
    public void Parse_Zero_CorrectValue()
    {
        HexBigInteger value = HexBigInteger.Parse("0x0");
        Assert.AreEqual(HexBigInteger.Zero, value);
    }

    [TestMethod]
    public void Parse_NegativeHex_CorrectValue()
    {
        HexBigInteger value = HexBigInteger.Parse("-0xff");
        Assert.AreEqual("-0xff", value.ToString());
        Assert.IsTrue(value.IsNegative);
    }

    [TestMethod]
    public void Parse_EmptyAfterPrefix_ReturnsZero()
    {
        HexBigInteger value = HexBigInteger.Parse("0x");
        Assert.AreEqual(HexBigInteger.Zero, value);
    }

    [TestMethod]
    public void Parse_UpperCase_Works()
    {
        HexBigInteger value = HexBigInteger.Parse("0xABCDEF");
        Assert.AreEqual("0xabcdef", value.ToString());
    }

    [TestMethod]
    public void TryParse_Valid_ReturnsTrue()
    {
        bool success = HexBigInteger.TryParse("0x1234", out HexBigInteger result);
        Assert.IsTrue(success);
        Assert.AreEqual("0x1234", result.ToString());
    }

    [TestMethod]
    public void TryParse_Invalid_ReturnsFalse()
    {
        bool success = HexBigInteger.TryParse("not-hex", out HexBigInteger result);
        Assert.IsFalse(success);
        Assert.AreEqual(HexBigInteger.Zero, result);
    }

    [TestMethod]
    public void TryParse_Null_ReturnsFalse()
    {
        bool success = HexBigInteger.TryParse(null, out HexBigInteger result);
        Assert.IsFalse(success);
        Assert.AreEqual(HexBigInteger.Zero, result);
    }

    [TestMethod]
    public void TryParse_Empty_ReturnsFalse()
    {
        bool success = HexBigInteger.TryParse("", out HexBigInteger result);
        Assert.IsFalse(success);
    }

    #endregion

    #region Byte Conversion Tests

    [TestMethod]
    public void ToBigEndianBytes_SmallValue_CorrectBytes()
    {
        HexBigInteger value = new(0x1234UL);
        byte[] bytes = value.ToBigEndianBytes();

        CollectionAssert.AreEqual(new byte[] { 0x12, 0x34 }, bytes);
    }

    [TestMethod]
    public void ToBigEndianBytes_WithPadding_CorrectBytes()
    {
        HexBigInteger value = new(0x42UL);
        byte[] bytes = value.ToBigEndianBytes(4);

        Assert.AreEqual(4, bytes.Length);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x42 }, bytes);
    }

    [TestMethod]
    public void ToBigEndianBytes_RoundTrip_PreservesValue()
    {
        var original = HexBigInteger.Parse("0xdeadbeefcafebabe12345678");
        byte[] bytes = original.ToBigEndianBytes();
        HexBigInteger restored = new(bytes);

        Assert.AreEqual(original, restored);
    }

    [TestMethod]
    public void WriteBigEndian_SmallValue_ReturnsCorrectCount()
    {
        HexBigInteger value = new(0x1234UL);
        Span<byte> buffer = stackalloc byte[32];
        int written = value.WriteBigEndian(buffer);

        Assert.AreEqual(2, written);
        Assert.AreEqual(0x12, buffer[0]);
        Assert.AreEqual(0x34, buffer[1]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void WriteBigEndian_BufferTooSmall_Throws()
    {
        var large = HexBigInteger.Parse("0x" + new string('f', 64));
        Span<byte> small = stackalloc byte[4];
        large.WriteBigEndian(small);
    }

    #endregion

    #region ToString Tests

    [TestMethod]
    public void ToString_Zero_ReturnsHexZero()
    {
        Assert.AreEqual("0x0", HexBigInteger.Zero.ToString());
    }

    [TestMethod]
    public void ToString_One_ReturnsHexOne()
    {
        Assert.AreEqual("0x1", HexBigInteger.One.ToString());
    }

    [TestMethod]
    public void ToString_SmallValue_MinimalHex()
    {
        HexBigInteger value = new(255);
        Assert.AreEqual("0xff", value.ToString());
    }

    [TestMethod]
    public void ToString_Negative_CorrectFormat()
    {
        HexBigInteger value = new(-255);
        Assert.AreEqual("-0xff", value.ToString());
    }

    [TestMethod]
    public void ToHexString_NoPrefix()
    {
        HexBigInteger value = new(0x42);
        Assert.AreEqual("42", value.ToHexString());
    }

    [TestMethod]
    public void ToHexString_WithMinDigits_PadsCorrectly()
    {
        HexBigInteger value = new(0x42);
        Assert.AreEqual("0042", value.ToHexString(4));
    }

    [TestMethod]
    public void ToHexString_ValueExceedsMinDigits_NoPadding()
    {
        HexBigInteger value = new(0x12345);
        Assert.AreEqual("12345", value.ToHexString(2));
    }

    #endregion

    #region Property Tests

    [TestMethod]
    public void IsZero_Zero_ReturnsTrue()
    {
        Assert.IsTrue(HexBigInteger.Zero.IsZero);
        Assert.IsTrue(new HexBigInteger(0).IsZero);
        Assert.IsTrue(default(HexBigInteger).IsZero);
    }

    [TestMethod]
    public void IsZero_NonZero_ReturnsFalse()
    {
        Assert.IsFalse(HexBigInteger.One.IsZero);
        Assert.IsFalse(new HexBigInteger(1).IsZero);
    }

    [TestMethod]
    public void IsOne_One_ReturnsTrue()
    {
        Assert.IsTrue(HexBigInteger.One.IsOne);
        Assert.IsTrue(new HexBigInteger(1).IsOne);
    }

    [TestMethod]
    public void IsOne_NotOne_ReturnsFalse()
    {
        Assert.IsFalse(HexBigInteger.Zero.IsOne);
        Assert.IsFalse(new HexBigInteger(2).IsOne);
    }

    [TestMethod]
    public void IsNegative_NegativeValue_ReturnsTrue()
    {
        HexBigInteger value = new(-42);
        Assert.IsTrue(value.IsNegative);
    }

    [TestMethod]
    public void IsNegative_PositiveValue_ReturnsFalse()
    {
        HexBigInteger value = new(42);
        Assert.IsFalse(value.IsNegative);
    }

    [TestMethod]
    public void Sign_CorrectValues()
    {
        Assert.AreEqual(-1, new HexBigInteger(-42).Sign);
        Assert.AreEqual(0, HexBigInteger.Zero.Sign);
        Assert.AreEqual(1, new HexBigInteger(42).Sign);
    }

    [TestMethod]
    public void ByteCount_CorrectValue()
    {
        Assert.AreEqual(0, HexBigInteger.Zero.ByteCount);
        Assert.AreEqual(1, HexBigInteger.One.ByteCount);
        Assert.AreEqual(2, new HexBigInteger(0x1234).ByteCount);
    }

    #endregion

    #region Static Constants Tests

    [TestMethod]
    public void Zero_IsCorrect()
    {
        Assert.IsTrue(HexBigInteger.Zero.IsZero);
        Assert.AreEqual("0x0", HexBigInteger.Zero.ToString());
    }

    [TestMethod]
    public void One_IsCorrect()
    {
        Assert.IsTrue(HexBigInteger.One.IsOne);
        Assert.AreEqual("0x1", HexBigInteger.One.ToString());
    }

    #endregion

    #region Blockchain Specific Tests

    [TestMethod]
    public void EVM_Wei_LargeValue()
    {
        // Max uint256 in EVM: 2^256 - 1
        string maxHex = new string('f', 64);
        HexBigInteger maxValue = HexBigInteger.Parse(maxHex);
        Assert.AreEqual("0x" + maxHex, maxValue.ToString());
    }

    [TestMethod]
    public void EVM_TokenAmount_RoundTrip()
    {
        // 1 ETH = 10^18 wei
        HexBigInteger oneEth = HexBigInteger.Parse("0xde0b6b3a7640000"); // 10^18 in hex

        byte[] bytes = oneEth.ToBigEndianBytes(32);
        HexBigInteger restored = new(bytes);

        Assert.AreEqual(oneEth, restored);
    }

    [TestMethod]
    public void Solana_Lamports_RoundTrip()
    {
        // 1 SOL = 10^9 lamports
        HexBigInteger oneSol = new(1_000_000_000UL);

        byte[] bytes = oneSol.ToBigEndianBytes(8);
        HexBigInteger restored = new(bytes);

        Assert.AreEqual(oneSol, restored);
        Assert.AreEqual((ulong)oneSol, 1_000_000_000UL);
    }

    [TestMethod]
    public void Comparison_Sorting_Works()
    {
        HexBigInteger[] values =
        [
            HexBigInteger.Parse("0x100"),
            HexBigInteger.Parse("0x1"),
            HexBigInteger.Parse("0xff"),
            HexBigInteger.Parse("0x10"),
            HexBigInteger.Parse("0x" + new string('f', 64)),
            HexBigInteger.Zero
        ];

        Array.Sort(values);

        Assert.AreEqual(HexBigInteger.Zero, values[0]);
        Assert.AreEqual(HexBigInteger.Parse("0x1"), values[1]);
        Assert.AreEqual(HexBigInteger.Parse("0x10"), values[2]);
        Assert.AreEqual(HexBigInteger.Parse("0xff"), values[3]);
        Assert.AreEqual(HexBigInteger.Parse("0x100"), values[4]);
        Assert.AreEqual(HexBigInteger.Parse("0x" + new string('f', 64)), values[5]);
    }

    [TestMethod]
    public void EVM_BalanceOf_Typical()
    {
        // Typical ERC20 balance response
        HexBigInteger balance = HexBigInteger.Parse("0x00000000000000000000000000000000000000000000003635c9adc5dea00000");
        Assert.AreEqual("0x3635c9adc5dea00000", balance.ToString());
    }

    #endregion

    #region Integration with uint256 Tests

    [TestMethod]
    public void ConvertFrom_uint256_ViaBytes()
    {
        uint256 u256 = uint256.Parse("0xdeadbeefcafebabe");
        byte[] bytes = u256.ToBigEndianBytes();
        HexBigInteger hbi = new(bytes);

        Assert.AreEqual(u256.ToString(), hbi.ToString());
    }

    [TestMethod]
    public void ConvertTo_uint256_ViaBytes()
    {
        HexBigInteger hbi = HexBigInteger.Parse("0xdeadbeefcafebabe");
        byte[] bytes = hbi.ToBigEndianBytes(32);
        uint256 u256 = new(bytes);

        Assert.AreEqual(hbi.ToString(), u256.ToString());
    }

    #endregion
}
