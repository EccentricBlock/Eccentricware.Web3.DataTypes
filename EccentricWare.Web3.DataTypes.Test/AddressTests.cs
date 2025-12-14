using System.Runtime.InteropServices;
using System.Text.Json;
using EccentricWare.Web3.DataTypes;

namespace EccentricWare.Web3.DataTypes.Test;

[TestClass]
public sealed class AddressTests
{
    // Well-known EVM test addresses
    private const string EvmZeroAddress = "0x0000000000000000000000000000000000000000";
    private const string EvmTestAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f2bD53";
    private const string EvmVitalikAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
    
    // Well-known Solana test addresses
    private const string SolanaSystemProgram = "11111111111111111111111111111111";
    private const string SolanaTokenProgram = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string SolanaTestAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";

    #region Construction Tests

    [TestMethod]
    public void Default_IsZeroEvm()
    {
        Address value = default;
        Assert.IsTrue(value.IsZero);
        Assert.AreEqual(AddressType.Evm, value.Type);
    }

    [TestMethod]
    public void ZeroEvm_IsCorrect()
    {
        Address zero = Address.ZeroEvm;
        Assert.IsTrue(zero.IsZero);
        Assert.AreEqual(AddressType.Evm, zero.Type);
        Assert.AreEqual(EvmZeroAddress, zero.ToString());
    }

    [TestMethod]
    public void ZeroSolana_IsCorrect()
    {
        Address zero = Address.ZeroSolana;
        Assert.IsTrue(zero.IsZero);
        Assert.AreEqual(AddressType.Solana, zero.Type);
        Assert.AreEqual(SolanaSystemProgram, zero.ToString());
    }

    [TestMethod]
    public void FromEvmBytes_CorrectValue()
    {
        byte[] bytes = new byte[20];
        bytes[0] = 0x74;
        bytes[19] = 0x53;

        Address addr = Address.FromEvmBytes(bytes);
        Assert.AreEqual(AddressType.Evm, addr.Type);
        Assert.IsFalse(addr.IsZero);
        Assert.AreEqual(20, addr.ByteLength);
    }

    [TestMethod]
    public void FromEvmBytes_RoundTrip()
    {
        byte[] original = new byte[20];
        for (int i = 0; i < 20; i++)
            original[i] = (byte)(i + 1);

        Address addr = Address.FromEvmBytes(original);
        byte[] result = addr.ToBytes();

        CollectionAssert.AreEqual(original, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void FromEvmBytes_WrongLength_Throws()
    {
        byte[] bytes = new byte[32];
        Address.FromEvmBytes(bytes);
    }

    [TestMethod]
    public void FromSolanaBytes_CorrectValue()
    {
        byte[] bytes = new byte[32];
        bytes[0] = 0x01;
        bytes[31] = 0xFF;

        Address addr = Address.FromSolanaBytes(bytes);
        Assert.AreEqual(AddressType.Solana, addr.Type);
        Assert.IsFalse(addr.IsZero);
        Assert.AreEqual(32, addr.ByteLength);
    }

    [TestMethod]
    public void FromSolanaBytes_RoundTrip()
    {
        byte[] original = new byte[32];
        for (int i = 0; i < 32; i++)
            original[i] = (byte)(i + 1);

        Address addr = Address.FromSolanaBytes(original);
        byte[] result = addr.ToBytes();

        CollectionAssert.AreEqual(original, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void FromSolanaBytes_WrongLength_Throws()
    {
        byte[] bytes = new byte[20];
        Address.FromSolanaBytes(bytes);
    }

    #endregion

    #region Equality Tests

    [TestMethod]
    public void Equals_SameEvmValue_ReturnsTrue()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        Address b = Address.ParseEvm(EvmTestAddress);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_SameSolanaValue_ReturnsTrue()
    {
        Address a = Address.ParseSolana(SolanaTokenProgram);
        Address b = Address.ParseSolana(SolanaTokenProgram);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_DifferentEvmValues_ReturnsFalse()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        Address b = Address.ParseEvm(EvmVitalikAddress);

        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Equals_DifferentTypes_ReturnsFalse()
    {
        // Zero EVM and Zero Solana should not be equal (different types)
        Address evmZero = Address.ZeroEvm;
        Address solanaZero = Address.ZeroSolana;

        Assert.IsFalse(evmZero.Equals(solanaZero));
        Assert.IsFalse(evmZero == solanaZero);
    }

    [TestMethod]
    public void Equals_Object_CorrectBehavior()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        object b = Address.ParseEvm(EvmTestAddress);
        object c = "not an Address";

        Assert.IsTrue(a.Equals(b));
        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualValues_SameHash()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        Address b = Address.ParseEvm(EvmTestAddress);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void GetHashCode_DifferentValues_DifferentHash()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        Address b = Address.ParseEvm(EvmVitalikAddress);

        Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<Address, string>();
        Address addr = Address.ParseEvm(EvmTestAddress);

        dict[addr] = "test";
        Assert.AreEqual("test", dict[addr]);

        Address sameAddr = Address.ParseEvm(EvmTestAddress);
        Assert.IsTrue(dict.ContainsKey(sameAddr));
    }

    #endregion

    #region Comparison Tests

    [TestMethod]
    public void CompareTo_EvmBeforeSolana()
    {
        Address evm = Address.ParseEvm(EvmTestAddress);
        Address solana = Address.ParseSolana(SolanaTokenProgram);

        Assert.IsTrue(evm.CompareTo(solana) < 0);
        Assert.IsTrue(evm < solana);
    }

    [TestMethod]
    public void CompareTo_SameType_LexicographicOrder()
    {
        Address smaller = Address.ParseEvm("0x0000000000000000000000000000000000000001");
        Address larger = Address.ParseEvm("0x0000000000000000000000000000000000000002");

        Assert.IsTrue(smaller.CompareTo(larger) < 0);
        Assert.IsTrue(smaller < larger);
        Assert.IsTrue(larger > smaller);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        Address b = Address.ParseEvm(EvmTestAddress);

        Assert.AreEqual(0, a.CompareTo(b));
        Assert.IsTrue(a <= b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_Object_CorrectBehavior()
    {
        Address a = Address.ParseEvm("0x0000000000000000000000000000000000000001");
        object b = Address.ParseEvm("0x0000000000000000000000000000000000000002");

        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(a.CompareTo(null) > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CompareTo_InvalidObject_Throws()
    {
        Address a = Address.ParseEvm(EvmTestAddress);
        a.CompareTo("invalid");
    }

    [TestMethod]
    public void Sorting_Works()
    {
        Address[] addresses =
        [
            Address.ParseEvm("0x3000000000000000000000000000000000000000"),
            Address.ParseEvm("0x1000000000000000000000000000000000000000"),
            Address.ParseSolana(SolanaTokenProgram),
            Address.ZeroEvm
        ];

        Array.Sort(addresses);

        Assert.AreEqual(Address.ZeroEvm, addresses[0]);
        Assert.AreEqual(AddressType.Evm, addresses[1].Type);
        Assert.AreEqual(AddressType.Evm, addresses[2].Type);
        Assert.AreEqual(AddressType.Solana, addresses[3].Type);
    }

    #endregion

    #region EVM Parse Tests

    [TestMethod]
    public void ParseEvm_WithPrefix_CorrectValue()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Assert.AreEqual(AddressType.Evm, addr.Type);
        Assert.AreEqual(EvmTestAddress.ToLowerInvariant(), addr.ToString());
    }

    [TestMethod]
    public void ParseEvm_WithoutPrefix_CorrectValue()
    {
        Address addr = Address.ParseEvm("742d35Cc6634C0532925a3b844Bc9e7595f2bD53");
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void ParseEvm_Lowercase_CorrectValue()
    {
        Address addr = Address.ParseEvm(EvmTestAddress.ToLowerInvariant());
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void ParseEvm_Uppercase_CorrectValue()
    {
        Address addr = Address.ParseEvm(EvmTestAddress.ToUpperInvariant().Replace("0X", "0x"));
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void ParseEvm_ZeroAddress_IsZero()
    {
        Address addr = Address.ParseEvm(EvmZeroAddress);
        Assert.IsTrue(addr.IsZero);
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseEvm_TooShort_Throws()
    {
        Address.ParseEvm("0x1234");
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseEvm_TooLong_Throws()
    {
        Address.ParseEvm("0x" + new string('a', 42));
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseEvm_InvalidCharacters_Throws()
    {
        Address.ParseEvm("0xgggggggggggggggggggggggggggggggggggggggg");
    }

    [TestMethod]
    public void TryParseEvm_Valid_ReturnsTrue()
    {
        bool success = Address.TryParseEvm(EvmTestAddress, out Address result);
        Assert.IsTrue(success);
        Assert.AreEqual(AddressType.Evm, result.Type);
    }

    [TestMethod]
    public void TryParseEvm_Invalid_ReturnsFalse()
    {
        bool success = Address.TryParseEvm("not-an-address", out Address result);
        Assert.IsFalse(success);
        Assert.AreEqual(Address.Zero, result);
    }

    #endregion

    #region Solana Parse Tests

    [TestMethod]
    public void ParseSolana_SystemProgram_CorrectValue()
    {
        Address addr = Address.ParseSolana(SolanaSystemProgram);
        Assert.AreEqual(AddressType.Solana, addr.Type);
        Assert.IsTrue(addr.IsZero);
    }

    [TestMethod]
    public void ParseSolana_TokenProgram_CorrectValue()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        Assert.AreEqual(AddressType.Solana, addr.Type);
        Assert.IsFalse(addr.IsZero);
    }

    [TestMethod]
    public void ParseSolana_RoundTrip()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        string result = addr.ToString();
        Address parsed = Address.ParseSolana(result);

        Assert.AreEqual(addr, parsed);
    }

    [TestMethod]
    public void ParseSolana_TestAddress_RoundTrip()
    {
        Address addr = Address.ParseSolana(SolanaTestAddress);
        string result = addr.ToString();
        Address parsed = Address.ParseSolana(result);

        Assert.AreEqual(addr, parsed);
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseSolana_Empty_Throws()
    {
        Address.ParseSolana("");
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseSolana_InvalidCharacter_Throws()
    {
        // '0' is not in Base58 alphabet
        Address.ParseSolana("0InvalidBase58");
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void ParseSolana_TooLong_Throws()
    {
        Address.ParseSolana(new string('1', 50));
    }

    [TestMethod]
    public void TryParseSolana_Valid_ReturnsTrue()
    {
        bool success = Address.TryParseSolana(SolanaTokenProgram, out Address result);
        Assert.IsTrue(success);
        Assert.AreEqual(AddressType.Solana, result.Type);
    }

    [TestMethod]
    public void TryParseSolana_Invalid_ReturnsFalse()
    {
        bool success = Address.TryParseSolana("0invalid", out Address result);
        Assert.IsFalse(success);
    }

    #endregion

    #region Auto-Detection Parse Tests

    [TestMethod]
    public void Parse_EvmWithPrefix_DetectsEvm()
    {
        Address addr = Address.Parse(EvmTestAddress);
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void Parse_Base58_DetectsSolana()
    {
        Address addr = Address.Parse(SolanaTokenProgram);
        Assert.AreEqual(AddressType.Solana, addr.Type);
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(Address.TryParse(null, out _));
        Assert.IsFalse(Address.TryParse("", out _));
    }

    #endregion

    #region Byte Conversion Tests

    [TestMethod]
    public void ToBytes_Evm_Returns20Bytes()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        byte[] bytes = addr.ToBytes();

        Assert.AreEqual(20, bytes.Length);
    }

    [TestMethod]
    public void ToBytes_Solana_Returns32Bytes()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        byte[] bytes = addr.ToBytes();

        Assert.AreEqual(32, bytes.Length);
    }

    [TestMethod]
    public void WriteBytes_Evm_CorrectBytes()
    {
        Address addr = Address.ParseEvm("0x742d35Cc6634C0532925a3b844Bc9e7595f2bD53");
        byte[] bytes = addr.ToBytes();

        Assert.AreEqual(0x74, bytes[0]);
        Assert.AreEqual(0x2d, bytes[1]);
        Assert.AreEqual(0x53, bytes[19]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void WriteBytes_DestinationTooSmall_Throws()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        byte[] small = new byte[10];
        addr.WriteBytes(small);
    }

    #endregion

    #region ToString Tests

    [TestMethod]
    public void ToString_Evm_ReturnsHexWithPrefix()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        string result = addr.ToString();

        Assert.AreEqual(42, result.Length); // 0x + 40 hex chars
        Assert.IsTrue(result.StartsWith("0x"));
    }

    [TestMethod]
    public void ToString_Evm_ZeroAddress()
    {
        string result = Address.ZeroEvm.ToString();
        Assert.AreEqual(EvmZeroAddress, result);
    }

    [TestMethod]
    public void ToString_Format_x_LowercaseNoPrefix()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        string result = addr.ToString("x");

        Assert.AreEqual(40, result.Length);
        Assert.IsFalse(result.StartsWith("0x"));
        Assert.AreEqual(result, result.ToLowerInvariant());
    }

    [TestMethod]
    public void ToString_Format_X_UppercaseNoPrefix()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        string result = addr.ToString("X");

        Assert.AreEqual(40, result.Length);
        Assert.IsFalse(result.StartsWith("0x"));
        Assert.AreEqual(result, result.ToUpperInvariant());
    }

    [TestMethod]
    public void ToString_Solana_ReturnsBase58()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        string result = addr.ToString();

        Assert.IsFalse(result.StartsWith("0x"));
        Assert.AreEqual(SolanaTokenProgram, result);
    }

    #endregion

    #region TryFormat Tests

    [TestMethod]
    public void TryFormat_Evm_CorrectOutput()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Span<char> buffer = stackalloc char[50];

        bool success = addr.TryFormat(buffer, out int written);

        Assert.IsTrue(success);
        Assert.AreEqual(42, written);
    }

    [TestMethod]
    public void TryFormat_Evm_BufferTooSmall_ReturnsFalse()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Span<char> buffer = stackalloc char[10];

        bool success = addr.TryFormat(buffer, out int written);

        Assert.IsFalse(success);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryFormat_Solana_CorrectOutput()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        Span<char> buffer = stackalloc char[50];

        bool success = addr.TryFormat(buffer, out int written);

        Assert.IsTrue(success);
        Assert.IsTrue(written > 0 && written <= 44);
    }

    [TestMethod]
    public void TryFormatUtf8_CorrectOutput()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Span<byte> buffer = stackalloc byte[50];

        bool success = addr.TryFormat(buffer, out int written);

        Assert.IsTrue(success);
        Assert.AreEqual(42, written);
    }

    #endregion

    #region Property Tests

    [TestMethod]
    public void Type_Evm_ReturnsEvm()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void Type_Solana_ReturnsSolana()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        Assert.AreEqual(AddressType.Solana, addr.Type);
    }

    [TestMethod]
    public void ByteLength_Evm_Returns20()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        Assert.AreEqual(20, addr.ByteLength);
    }

    [TestMethod]
    public void ByteLength_Solana_Returns32()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        Assert.AreEqual(32, addr.ByteLength);
    }

    [TestMethod]
    public void IsZero_ZeroEvm_ReturnsTrue()
    {
        Assert.IsTrue(Address.ZeroEvm.IsZero);
        Assert.IsTrue(Address.ParseEvm(EvmZeroAddress).IsZero);
    }

    [TestMethod]
    public void IsZero_NonZero_ReturnsFalse()
    {
        Assert.IsFalse(Address.ParseEvm(EvmTestAddress).IsZero);
        Assert.IsFalse(Address.ParseSolana(SolanaTokenProgram).IsZero);
    }

    #endregion

    #region Conversion Tests

    [TestMethod]
    public void ToHash32_Solana_Works()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        Hash32 hash = addr.ToHash32();

        byte[] addrBytes = addr.ToBytes();
        byte[] hashBytes = hash.ToBigEndianBytes();

        CollectionAssert.AreEqual(addrBytes, hashBytes);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ToHash32_Evm_Throws()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        addr.ToHash32();
    }

    [TestMethod]
    public void FromHash32_RoundTrip()
    {
        Address original = Address.ParseSolana(SolanaTokenProgram);
        Hash32 hash = original.ToHash32();
        Address restored = Address.FromHash32(hash);

        Assert.AreEqual(original, restored);
    }

    #endregion

    #region Memory Layout Tests

    [TestMethod]
    public void StructSize_Is33Bytes()
    {
        // 4 x ulong (32 bytes) + 1 byte for AddressType = 33 bytes with Pack=1
        Assert.AreEqual(33, Marshal.SizeOf<Address>());
    }

    #endregion

    #region JSON Serialization Tests

    [TestMethod]
    public void JsonSerialize_Evm_CorrectFormat()
    {
        Address addr = Address.ParseEvm(EvmTestAddress);
        string json = JsonSerializer.Serialize(addr);

        Assert.AreEqual($"\"{EvmTestAddress.ToLowerInvariant()}\"", json);
    }

    [TestMethod]
    public void JsonSerialize_Solana_CorrectFormat()
    {
        Address addr = Address.ParseSolana(SolanaTokenProgram);
        string json = JsonSerializer.Serialize(addr);

        Assert.AreEqual($"\"{SolanaTokenProgram}\"", json);
    }

    [TestMethod]
    public void JsonDeserialize_Evm_CorrectValue()
    {
        string json = $"\"{EvmTestAddress}\"";
        Address addr = JsonSerializer.Deserialize<Address>(json);

        Assert.AreEqual(AddressType.Evm, addr.Type);
        Assert.AreEqual(EvmTestAddress.ToLowerInvariant(), addr.ToString());
    }

    [TestMethod]
    public void JsonDeserialize_Solana_CorrectValue()
    {
        string json = $"\"{SolanaTokenProgram}\"";
        Address addr = JsonSerializer.Deserialize<Address>(json);

        Assert.AreEqual(AddressType.Solana, addr.Type);
        Assert.AreEqual(SolanaTokenProgram, addr.ToString());
    }

    [TestMethod]
    public void JsonRoundTrip_Evm_PreservesValue()
    {
        Address original = Address.ParseEvm(EvmTestAddress);
        string json = JsonSerializer.Serialize(original);
        Address restored = JsonSerializer.Deserialize<Address>(json);

        Assert.AreEqual(original, restored);
    }

    [TestMethod]
    public void JsonRoundTrip_Solana_PreservesValue()
    {
        Address original = Address.ParseSolana(SolanaTokenProgram);
        string json = JsonSerializer.Serialize(original);
        Address restored = JsonSerializer.Deserialize<Address>(json);

        Assert.AreEqual(original, restored);
    }

    #endregion

    #region Blockchain Specific Tests

    [TestMethod]
    public void Evm_CommonAddresses()
    {
        // WETH on Ethereum mainnet
        Address weth = Address.ParseEvm("0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");
        Assert.AreEqual(AddressType.Evm, weth.Type);
        Assert.IsFalse(weth.IsZero);

        // USDC on Ethereum mainnet
        Address usdc = Address.ParseEvm("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
        Assert.AreEqual(AddressType.Evm, usdc.Type);
    }

    [TestMethod]
    public void Solana_CommonAddresses()
    {
        // System Program
        Address system = Address.ParseSolana(SolanaSystemProgram);
        Assert.AreEqual(AddressType.Solana, system.Type);
        Assert.IsTrue(system.IsZero);

        // Token Program
        Address token = Address.ParseSolana(SolanaTokenProgram);
        Assert.AreEqual(AddressType.Solana, token.Type);
        Assert.IsFalse(token.IsZero);
    }

    [TestMethod]
    public void Solana_Base58_LeadingOnes()
    {
        // Test address with leading 1s (representing leading zero bytes)
        Address addr = Address.ParseSolana(SolanaSystemProgram);
        Assert.IsTrue(addr.IsZero);
        Assert.AreEqual(SolanaSystemProgram, addr.ToString());
    }

    #endregion

    #region SpanParsable Interface Tests

    [TestMethod]
    public void ParseWithProvider_Works()
    {
        Address addr = Address.Parse(EvmTestAddress, null);
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void TryParseWithProvider_Works()
    {
        bool success = Address.TryParse(EvmTestAddress, null, out Address result);
        Assert.IsTrue(success);
        Assert.AreEqual(AddressType.Evm, result.Type);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Parse_MixedCase_Evm_Works()
    {
        // Mixed case should parse correctly
        Address addr = Address.ParseEvm("0xAbCdEf0123456789AbCdEf0123456789AbCdEf01");
        Assert.AreEqual(AddressType.Evm, addr.Type);
    }

    [TestMethod]
    public void Solana_MaxLengthAddress_Works()
    {
        // Generate a valid 32-byte address and encode it
        byte[] bytes = new byte[32];
        for (int i = 0; i < 32; i++)
            bytes[i] = 0xFF; // Max values

        Address addr = Address.FromSolanaBytes(bytes);
        string encoded = addr.ToString();
        
        Assert.IsTrue(encoded.Length <= 44);
        
        Address parsed = Address.ParseSolana(encoded);
        Assert.AreEqual(addr, parsed);
    }

    [TestMethod]
    public void Solana_SingleByteValue_Works()
    {
        // Address with only last byte set
        byte[] bytes = new byte[32];
        bytes[31] = 1;

        Address addr = Address.FromSolanaBytes(bytes);
        string encoded = addr.ToString();
        Address parsed = Address.ParseSolana(encoded);

        Assert.AreEqual(addr, parsed);
    }

    #endregion
}

