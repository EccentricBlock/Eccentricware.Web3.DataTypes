using EccentricWare.Web3.DataTypes;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace EccentricWare.Web3.DataTypes.Tests;

/// <summary>
/// Chain-specific behavioural tests for uint256 in EVM and Solana contexts.
/// Focus: JSON-RPC token shapes, ABI word encoding, and canonical constants.
/// </summary>
[TestClass]
public sealed class UInt256EvmSolanaTests
{
    private static readonly BigInteger TenPow18 = BigInteger.Pow(10, 18);
    private static readonly BigInteger TenPow9 = BigInteger.Pow(10, 9);

    [TestMethod]
    public void Evm_WeiPerEther_And_WeiPerGwei_Constants_AreCorrect()
    {
        Assert.AreEqual(TenPow18, uint256.Evm.WeiPerEther.ToBigInteger());
        Assert.AreEqual(TenPow9, uint256.Evm.WeiPerGwei.ToBigInteger());

        // Canonical EVM-style minimal hex (lowercase).
        Assert.AreEqual("0xde0b6b3a7640000", uint256.Evm.WeiPerEther.ToEvmHex());
        Assert.AreEqual("0x3b9aca00", uint256.Evm.WeiPerGwei.ToEvmHex());
    }

    [TestMethod]
    public void Solana_LamportsPerSol_Constant_IsCorrect()
    {
        Assert.AreEqual(TenPow9, uint256.Solana.LamportsPerSol.ToBigInteger());
        Assert.AreEqual("0x3b9aca00", uint256.Solana.LamportsPerSol.ToString("0x", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Evm_ToEvmHex_IsMinimal_NoLeadingZeros()
    {
        Assert.AreEqual("0x0", uint256.Zero.ToEvmHex());
        Assert.AreEqual("0x1", uint256.One.ToEvmHex());

        var v = uint256.Parse("0x0001".AsSpan(), CultureInfo.InvariantCulture);
        Assert.AreEqual(uint256.One, v);
        Assert.AreEqual("0x1", v.ToEvmHex());

        var v2 = uint256.Parse("0x0100".AsSpan(), CultureInfo.InvariantCulture);
        Assert.AreEqual(new BigInteger(256), v2.ToBigInteger());
        Assert.AreEqual("0x100", v2.ToEvmHex());
    }

    [TestMethod]
    public void Evm_JsonRpcHexTokens_CommonForms_Parse()
    {
        Assert.IsTrue(uint256.TryParse("0x0".AsSpan(), CultureInfo.InvariantCulture, out var a));
        Assert.AreEqual(uint256.Zero, a);

        Assert.IsTrue(uint256.TryParse("0x1".AsSpan(), CultureInfo.InvariantCulture, out var b));
        Assert.AreEqual(uint256.One, b);

        Assert.IsTrue(uint256.TryParse("0XDE0B6B3A7640000".AsSpan(), CultureInfo.InvariantCulture, out var c));
        Assert.AreEqual(uint256.Evm.WeiPerEther, c);

        // Non-canonical but frequently observed: "0x" => treat as zero.
        Assert.IsTrue(uint256.TryParse("0x".AsSpan(), CultureInfo.InvariantCulture, out var z));
        Assert.AreEqual(uint256.Zero, z);

        // Whitespace trimming.
        Assert.IsTrue(uint256.TryParse("   0x10   ".AsSpan(), CultureInfo.InvariantCulture, out var d));
        Assert.AreEqual(new BigInteger(16), d.ToBigInteger());
    }

    [TestMethod]
    public void Evm_AbiEncoding_Uint256_Is32ByteBigEndianWord_LeftPadded()
    {
        // ABI uint256 is a 32-byte big-endian word.
        byte[] abi = uint256.One.ToAbiEncoded();
        Assert.HasCount(32, abi);

        Assert.IsTrue(abi.Take(31).All(b => b == 0x00));
        Assert.AreEqual(0x01, abi[31]);

        // Round-trip.
        var rt = uint256.FromAbiEncoded(abi);
        Assert.AreEqual(uint256.One, rt);
    }

    [TestMethod]
    public void Evm_AbiEncoding_FromAbiEncoded_IgnoresExtraBytes()
    {
        var v = uint256.Parse("0x1234567890abcdef".AsSpan(), CultureInfo.InvariantCulture);
        byte[] abi = v.ToAbiEncoded();

        // Append extra bytes (e.g., concatenated ABI words) - FromAbiEncoded reads the first 32 bytes.
        byte[] extended = new byte[64];
        Buffer.BlockCopy(abi, 0, extended, 0, 32);
        // second word intentionally non-zero to ensure it is ignored
        extended[63] = 0xAA;

        var parsed = uint256.FromAbiEncoded(extended);
        Assert.AreEqual(v, parsed);
    }

    [TestMethod]
    public void Solana_Lamports_DivRem_ToSol_QuotientAndRemainder()
    {
        // 1.5 SOL expressed as lamports.
        var lamports = new uint256(1_500_000_000UL);
        var (q, r) = uint256.DivRem(lamports, uint256.Solana.LamportsPerSol);

        Assert.AreEqual(uint256.One, q);
        Assert.AreEqual(new uint256(500_000_000UL), r);

        // Also validate against BigInteger.
        BigInteger biLamports = lamports.ToBigInteger();
        BigInteger biDiv = uint256.Solana.LamportsPerSol.ToBigInteger();
        Assert.AreEqual(biLamports / biDiv, q.ToBigInteger());
        Assert.AreEqual(biLamports % biDiv, r.ToBigInteger());
    }

    [TestMethod]
    public void Solana_Lamports_TryMultiply_SolToLamports()
    {
        // Multiply 2 SOL (integer SOL units) into lamports (10^9).
        var sol = new uint256(2UL);

        Assert.IsTrue(sol.TryMultiply(1_000_000_000UL, out var lamports));
        Assert.AreEqual(new uint256(2_000_000_000UL), lamports);

        // Validate against the chain constant.
        Assert.IsTrue(sol.TryMultiply((ulong)uint256.Solana.LamportsPerSol.ToBigInteger(), out var lamports2));
        Assert.AreEqual(lamports, lamports2);
    }

    [TestMethod]
    public void Solana_JsonRpcDecimalTokens_Utf8_QuotedAndUnquoted_Parse()
    {
        // Solana JSON-RPC commonly returns decimals (slot, blockHeight, lamports) as JSON numbers or strings.
        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("123456789"), out var a));
        Assert.AreEqual(new BigInteger(123456789), a.ToBigInteger());

        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("\"123456789\""), out var b));
        Assert.AreEqual(a, b);

        // Quotes + internal ASCII whitespace should still parse (implementation trims after unquoting).
        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("\"  123456789  \""), out var c));
        Assert.AreEqual(a, c);

        // Decimal lamports example.
        Assert.IsTrue(uint256.TryParse(Encoding.UTF8.GetBytes("1000000000"), out var oneSolLamports));
        Assert.AreEqual(uint256.Solana.LamportsPerSol, oneSolLamports);
    }
}

/// <summary>
/// EVM-specific tests for int256 behaviour where two's complement interpretation is material
/// (ABI words and 64-hex-digit JSON/trace representations).
/// </summary>
[TestClass]
public sealed class Int256EvmSpecificTests
{
    private static readonly string HexMinusOne64Lower = new string('f', 64);
    private static readonly string HexMinusOne64Upper = new string('F', 64);
    private static readonly string HexMinValue64Lower = "8" + new string('0', 63);

    [TestMethod]
    public void Evm_AbiEncoding_Int256_TwosComplement_WordEncodings_AreCorrect()
    {
        Span<byte> be = stackalloc byte[32];

        // int256(1) => 31x00 + 0x01
        int256.One.WriteBigEndian(be);
        Assert.IsTrue(be.Slice(0, 31).ToArray().All(b => b == 0x00));
        Assert.AreEqual(0x01, be[31]);

        // int256(-1) => 32xFF
        int256.MinusOne.WriteBigEndian(be);
        Assert.IsTrue(be.ToArray().All(b => b == 0xFF));

        // int256.MinValue => 0x80 followed by 31x00
        int256.MinValue.WriteBigEndian(be);
        Assert.AreEqual(0x80, be[0]);
        Assert.IsTrue(be.Slice(1).ToArray().All(b => b == 0x00));
    }

    [TestMethod]
    public void Evm_Int256_HexFormatting_Negatives_AreFullWidthTwosComplement()
    {
        Assert.AreEqual("0x" + HexMinusOne64Lower, int256.MinusOne.ToString("0x", CultureInfo.InvariantCulture));
        Assert.AreEqual("0X" + HexMinusOne64Upper, int256.MinusOne.ToString("0X", CultureInfo.InvariantCulture));
        Assert.AreEqual("0x" + HexMinValue64Lower, int256.MinValue.ToString("0x", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Evm_Int256_Parse_64HexDigits_IsRawTwosComplement()
    {
        // Exactly 64 hex digits should be interpreted as raw two's complement (ABI-like).
        Assert.IsTrue(int256.TryParse(("0x" + HexMinusOne64Lower).AsSpan(), CultureInfo.InvariantCulture, out var m1));
        Assert.AreEqual(int256.MinusOne, m1);

        Assert.IsTrue(int256.TryParse(("0x" + HexMinValue64Lower).AsSpan(), CultureInfo.InvariantCulture, out var min));
        Assert.AreEqual(int256.MinValue, min);
    }
}
