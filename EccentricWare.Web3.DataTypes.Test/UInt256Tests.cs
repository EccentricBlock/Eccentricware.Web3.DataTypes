using EccentricWare.Web3.DataTypes;

namespace EccentricWare.Web3.DataTypes.Test;

[TestClass]
public sealed class UInt256Tests
{
    #region Construction Tests

    [TestMethod]
    public void Constructor_Default_IsZero()
    {
        uint256 value = default;
        Assert.IsTrue(value.IsZero);
        Assert.AreEqual(uint256.Zero, value);
    }

    [TestMethod]
    public void Constructor_FromUlong_CorrectValue()
    {
        uint256 value = new(0xDEADBEEFCAFEBABEUL);
        Assert.AreEqual("0xdeadbeefcafebabe", value.ToString());
    }

    [TestMethod]
    public void Constructor_FromFourUlongs_CorrectValue()
    {
        uint256 value = new(1, 2, 3, 4);
        Assert.IsFalse(value.IsZero);
    }

    [TestMethod]
    public void Constructor_FromBigEndianBytes_CorrectValue()
    {
        byte[] bytes = new byte[32];
        bytes[31] = 0x42; // Least significant byte
        
        uint256 value = new(bytes);
        Assert.AreEqual("0x42", value.ToString());
    }

    [TestMethod]
    public void Constructor_FromBigEndianBytes_MaxValue()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        uint256 value = new(bytes);
        Assert.AreEqual(uint256.MaxValue, value);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_FromBigEndianBytes_WrongLength_Throws()
    {
        byte[] bytes = new byte[16];
        _ = new uint256(bytes);
    }

    #endregion

    #region Equality Tests

    [TestMethod]
    public void Equals_SameValue_ReturnsTrue()
    {
        uint256 a = new(123456789UL);
        uint256 b = new(123456789UL);
        
        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        uint256 a = new(123UL);
        uint256 b = new(456UL);
        
        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Equals_LargeValues_CorrectComparison()
    {
        uint256 a = new(1, 2, 3, 4);
        uint256 b = new(1, 2, 3, 4);
        uint256 c = new(1, 2, 3, 5);
        
        Assert.IsTrue(a == b);
        Assert.IsFalse(a == c);
    }

    [TestMethod]
    public void Equals_Object_CorrectBehavior()
    {
        uint256 a = new(42);
        object b = new uint256(42);
        object c = "not a uint256";
        
        Assert.IsTrue(a.Equals(b));
        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualValues_SameHash()
    {
        uint256 a = new(0xDEADBEEF, 0xCAFEBABE, 0x12345678, 0x87654321);
        uint256 b = new(0xDEADBEEF, 0xCAFEBABE, 0x12345678, 0x87654321);
        
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    #endregion

    #region Comparison Tests

    [TestMethod]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        uint256 smaller = new(100);
        uint256 larger = new(200);
        
        Assert.IsTrue(smaller.CompareTo(larger) < 0);
        Assert.IsTrue(smaller < larger);
        Assert.IsTrue(smaller <= larger);
    }

    [TestMethod]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        uint256 smaller = new(100);
        uint256 larger = new(200);
        
        Assert.IsTrue(larger.CompareTo(smaller) > 0);
        Assert.IsTrue(larger > smaller);
        Assert.IsTrue(larger >= smaller);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        uint256 a = new(12345);
        uint256 b = new(12345);
        
        Assert.AreEqual(0, a.CompareTo(b));
        Assert.IsTrue(a <= b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_LargeValues_ComparesMostSignificantFirst()
    {
        // Same low parts, different high part
        uint256 smaller = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0);
        uint256 larger = new(0, 0, 0, 1);
        
        Assert.IsTrue(smaller < larger);
    }

    [TestMethod]
    public void CompareTo_Object_CorrectBehavior()
    {
        uint256 a = new(100);
        object b = new uint256(200);
        
        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(a.CompareTo(null) > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CompareTo_InvalidObject_Throws()
    {
        uint256 a = new(100);
        a.CompareTo("invalid");
    }

    [TestMethod]
    public void Comparison_AllOperators_Work()
    {
        uint256 a = new(10);
        uint256 b = new(20);
        uint256 c = new(10);
        
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
    public void ImplicitConversion_FromUlong_Works()
    {
        uint256 value = 0xCAFEBABEUL;
        Assert.AreEqual("0xcafebabe", value.ToString());
    }

    [TestMethod]
    public void ImplicitConversion_FromUint_Works()
    {
        uint256 value = 0xDEADBEEFU;
        Assert.AreEqual("0xdeadbeef", value.ToString());
    }

    [TestMethod]
    public void ExplicitConversion_ToUlong_SmallValue_Works()
    {
        uint256 value = new(12345UL);
        ulong result = (ulong)value;
        Assert.AreEqual(12345UL, result);
    }

    [TestMethod]
    [ExpectedException(typeof(OverflowException))]
    public void ExplicitConversion_ToUlong_LargeValue_Throws()
    {
        uint256 value = new(0, 1, 0, 0); // Value larger than ulong.MaxValue
        _ = (ulong)value;
    }

    #endregion

    #region Parse Tests

    [TestMethod]
    public void Parse_HexWithPrefix_CorrectValue()
    {
        uint256 value = uint256.Parse("0x1234");
        Assert.AreEqual("0x1234", value.ToString());
    }

    [TestMethod]
    public void Parse_HexWithoutPrefix_CorrectValue()
    {
        uint256 value = uint256.Parse("abcd");
        Assert.AreEqual("0xabcd", value.ToString());
    }

    [TestMethod]
    public void Parse_FullHex_CorrectValue()
    {
        string hex = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        uint256 value = uint256.Parse(hex);
        Assert.AreEqual(uint256.MaxValue, value);
    }

    [TestMethod]
    public void Parse_Zero_CorrectValue()
    {
        uint256 value = uint256.Parse("0x0");
        Assert.AreEqual(uint256.Zero, value);
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void Parse_TooLong_Throws()
    {
        uint256.Parse("0x" + new string('f', 65));
    }

    [TestMethod]
    public void TryParse_Valid_ReturnsTrue()
    {
        bool success = uint256.TryParse("0x1234", out uint256 result);
        Assert.IsTrue(success);
        Assert.AreEqual("0x1234", result.ToString());
    }

    [TestMethod]
    public void TryParse_Invalid_ReturnsFalse()
    {
        bool success = uint256.TryParse("not-hex", out uint256 result);
        Assert.IsFalse(success);
        Assert.AreEqual(uint256.Zero, result);
    }

    #endregion

    #region Byte Conversion Tests

    [TestMethod]
    public void ToBigEndianBytes_SmallValue_CorrectBytes()
    {
        uint256 value = new(0x42UL);
        byte[] bytes = value.ToBigEndianBytes();
        
        Assert.AreEqual(32, bytes.Length);
        Assert.AreEqual(0x42, bytes[31]); // Least significant byte last
        
        // All other bytes should be zero
        for (int i = 0; i < 31; i++)
            Assert.AreEqual(0, bytes[i]);
    }

    [TestMethod]
    public void ToBigEndianBytes_MaxValue_AllOnes()
    {
        byte[] bytes = uint256.MaxValue.ToBigEndianBytes();
        
        foreach (byte b in bytes)
            Assert.AreEqual(0xFF, b);
    }

    [TestMethod]
    public void WriteBigEndian_RoundTrip_PreservesValue()
    {
        uint256 original = new(0xDEADBEEF, 0xCAFEBABE, 0x12345678, 0x87654321);
        byte[] bytes = original.ToBigEndianBytes();
        uint256 restored = new(bytes);
        
        Assert.AreEqual(original, restored);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void WriteBigEndian_DestinationTooSmall_Throws()
    {
        uint256 value = new(42);
        byte[] small = new byte[16];
        value.WriteBigEndian(small);
    }

    #endregion

    #region ToString Tests

    [TestMethod]
    public void ToString_Zero_ReturnsHexZero()
    {
        Assert.AreEqual("0x0", uint256.Zero.ToString());
    }

    [TestMethod]
    public void ToString_One_ReturnsHexOne()
    {
        Assert.AreEqual("0x1", uint256.One.ToString());
    }

    [TestMethod]
    public void ToString_SmallValue_MinimalHex()
    {
        uint256 value = new(255);
        Assert.AreEqual("0xff", value.ToString());
    }

    [TestMethod]
    public void ToFullHexString_PadsToFullWidth()
    {
        uint256 value = new(0x42);
        string full = value.ToFullHexString();
        
        Assert.AreEqual(64, full.Length);
        Assert.IsTrue(full.EndsWith("42"));
        Assert.IsTrue(full.StartsWith("0000000000000000"));
    }

    [TestMethod]
    public void ToFullHexString_MaxValue_All64Fs()
    {
        string full = uint256.MaxValue.ToFullHexString();
        Assert.AreEqual(new string('f', 64), full);
    }

    #endregion

    #region Property Tests

    [TestMethod]
    public void IsZero_Zero_ReturnsTrue()
    {
        Assert.IsTrue(uint256.Zero.IsZero);
        Assert.IsTrue(new uint256(0).IsZero);
        Assert.IsTrue(default(uint256).IsZero);
    }

    [TestMethod]
    public void IsZero_NonZero_ReturnsFalse()
    {
        Assert.IsFalse(uint256.One.IsZero);
        Assert.IsFalse(uint256.MaxValue.IsZero);
        Assert.IsFalse(new uint256(1).IsZero);
    }

    [TestMethod]
    public void IsOne_One_ReturnsTrue()
    {
        Assert.IsTrue(uint256.One.IsOne);
        Assert.IsTrue(new uint256(1).IsOne);
    }

    [TestMethod]
    public void IsOne_NotOne_ReturnsFalse()
    {
        Assert.IsFalse(uint256.Zero.IsOne);
        Assert.IsFalse(new uint256(2).IsOne);
        Assert.IsFalse(uint256.MaxValue.IsOne);
    }

    [TestMethod]
    public void FitsInUlong_SmallValue_ReturnsTrue()
    {
        Assert.IsTrue(uint256.Zero.FitsInUlong);
        Assert.IsTrue(uint256.One.FitsInUlong);
        Assert.IsTrue(new uint256(ulong.MaxValue).FitsInUlong);
    }

    [TestMethod]
    public void FitsInUlong_LargeValue_ReturnsFalse()
    {
        Assert.IsFalse(uint256.MaxValue.FitsInUlong);
        Assert.IsFalse(new uint256(0, 1, 0, 0).FitsInUlong);
    }

    #endregion

    #region Static Constants Tests

    [TestMethod]
    public void Zero_IsCorrect()
    {
        Assert.IsTrue(uint256.Zero.IsZero);
        Assert.AreEqual("0x0", uint256.Zero.ToString());
    }

    [TestMethod]
    public void One_IsCorrect()
    {
        Assert.IsTrue(uint256.One.IsOne);
        Assert.AreEqual("0x1", uint256.One.ToString());
    }

    [TestMethod]
    public void MaxValue_IsCorrect()
    {
        string expected = "0x" + new string('f', 64);
        Assert.AreEqual(expected, uint256.MaxValue.ToString());
    }

    #endregion

    #region Blockchain Specific Tests

    [TestMethod]
    public void EVM_Wei_MaxValue()
    {
        // Max uint256 in EVM: 2^256 - 1
        uint256 maxValue = uint256.Parse("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        Assert.AreEqual(uint256.MaxValue, maxValue);
    }

    [TestMethod]
    public void EVM_TokenAmount_RoundTrip()
    {
        // 1 ETH = 10^18 wei
        uint256 oneEth = uint256.Parse("0xde0b6b3a7640000"); // 10^18 in hex
        
        byte[] bytes = oneEth.ToBigEndianBytes();
        uint256 restored = new(bytes);
        
        Assert.AreEqual(oneEth, restored);
    }

    [TestMethod]
    public void Solana_Lamports_RoundTrip()
    {
        // 1 SOL = 10^9 lamports
        uint256 oneSol = new(1_000_000_000UL);
        
        byte[] bytes = oneSol.ToBigEndianBytes();
        uint256 restored = new(bytes);
        
        Assert.AreEqual(oneSol, restored);
        Assert.AreEqual((ulong)oneSol, 1_000_000_000UL);
    }

    [TestMethod]
    public void Comparison_Sorting_Works()
    {
        uint256[] values = 
        [
            uint256.Parse("0x100"),
            uint256.Parse("0x1"),
            uint256.Parse("0xff"),
            uint256.Parse("0x10"),
            uint256.MaxValue,
            uint256.Zero
        ];

        Array.Sort(values);

        Assert.AreEqual(uint256.Zero, values[0]);
        Assert.AreEqual(uint256.Parse("0x1"), values[1]);
        Assert.AreEqual(uint256.Parse("0x10"), values[2]);
        Assert.AreEqual(uint256.Parse("0xff"), values[3]);
        Assert.AreEqual(uint256.Parse("0x100"), values[4]);
        Assert.AreEqual(uint256.MaxValue, values[5]);
    }

    #endregion

    #region Memory Layout Tests

    [TestMethod]
    public void StructSize_Is32Bytes()
    {
        // Verify the struct is exactly 32 bytes (4 x 8 bytes)
        Assert.AreEqual(32, System.Runtime.InteropServices.Marshal.SizeOf<uint256>());
    }

    #endregion
}

