using EccentricWare.Web3.DataTypes;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class AddressTests
{
    // -------------------------
    // Layout / invariants
    // -------------------------

    [TestMethod]
    public void Layout_Size_Is_40_Bytes()
    {
        // Explicit padding + Pack=8 intends to lock this at 40 bytes.
        int size = Marshal.SizeOf<Address>();
        Assert.AreEqual(40, size);
    }

    [TestMethod]
    public void Default_Is_ZeroEvm()
    {
        Address d = default;

        Assert.AreEqual(AddressType.Evm, d.Type);
        Assert.AreEqual(Address.EvmByteLength, d.ByteLength);
        Assert.IsTrue(d.IsZero);

        Assert.AreEqual(Address.ZeroEvm, d);
        Assert.AreEqual("0x" + new string('0', Address.EvmHexLength), d.ToString());
    }

    [TestMethod]
    public void ZeroSolana_ToString_Is_32_Ones_And_RoundTrips()
    {
        Address a = Address.ZeroSolana;

        Assert.AreEqual(AddressType.Solana, a.Type);
        Assert.AreEqual(Address.SolanaByteLength, a.ByteLength);
        Assert.IsTrue(a.IsZero);

        string s = a.ToString();
        Assert.AreEqual(new string('1', 32), s);

        Address parsed = Address.Parse(s);
        Assert.AreEqual(AddressType.Solana, parsed.Type);
        Assert.AreEqual(a, parsed);
    }

    // -------------------------
    // FromBytes / ToBytes / WriteBytes
    // -------------------------

    [TestMethod]
    public void FromEvmBytes_RoundTrips_ToBytes_And_ToString_Default()
    {
        // 0x52908400098527886e0f7030069857d2e4169ee7
        byte[] evm20 = HexToBytes("52908400098527886e0f7030069857d2e4169ee7");

        Address a = Address.FromEvmBytes(evm20);

        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual(Address.EvmByteLength, a.ByteLength);
        Assert.IsFalse(a.IsZero);

        byte[] round = a.ToBytes();
        CollectionAssert.AreEqual(evm20, round);

        Assert.AreEqual("0x52908400098527886e0f7030069857d2e4169ee7", a.ToString());
    }

    [TestMethod]
    public void FromEvmBytes_Invalid_Length_Throws()
    {
        Assert.Throws<ArgumentException>(() => Address.FromEvmBytes(new byte[0]));
        Assert.Throws<ArgumentException>(() => Address.FromEvmBytes(new byte[19]));
        Assert.Throws<ArgumentException>(() => Address.FromEvmBytes(new byte[21]));
    }

    [TestMethod]
    public void FromSolanaBytes_RoundTrips_ToBytes_And_Parse_Of_ToString()
    {
        byte[] sol32 = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Address a = Address.FromSolanaBytes(sol32);

        Assert.AreEqual(AddressType.Solana, a.Type);
        Assert.AreEqual(Address.SolanaByteLength, a.ByteLength);
        Assert.IsFalse(a.IsZero);

        byte[] round = a.ToBytes();
        CollectionAssert.AreEqual(sol32, round);

        // Ensure Base58 string is stable and round-trips.
        string s = a.ToString();
        Assert.IsTrue(s.Length is >= 32 and <= Address.MaxBase58Length);

        Address parsed = Address.Parse(s);
        Assert.AreEqual(AddressType.Solana, parsed.Type);
        Assert.AreEqual(a, parsed);
    }

    [TestMethod]
    public void FromSolanaBytes_Invalid_Length_Throws()
    {
        Assert.Throws<ArgumentException>(() => Address.FromSolanaBytes(new byte[0]));
        Assert.Throws<ArgumentException>(() => Address.FromSolanaBytes(new byte[31]));
        Assert.Throws<ArgumentException>(() => Address.FromSolanaBytes(new byte[33]));
    }

    [TestMethod]
    public void WriteBytes_Evm_Writes_Only_First_20_Bytes()
    {
        byte[] evm20 = HexToBytes("000102030405060708090a0b0c0d0e0f10111213");
        Address a = Address.FromEvmBytes(evm20);

        byte[] dest = Enumerable.Repeat((byte)0xCC, 64).ToArray();
        a.WriteBytes(dest);

        CollectionAssert.AreEqual(evm20, dest.Take(Address.EvmByteLength).ToArray());

        // Ensure bytes beyond required length are not overwritten.
        Assert.IsTrue(dest.Skip(Address.EvmByteLength).All(b => b == 0xCC));
    }

    [TestMethod]
    public void WriteBytes_Solana_Writes_Only_First_32_Bytes()
    {
        byte[] sol32 = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();
        Address a = Address.FromSolanaBytes(sol32);

        byte[] dest = Enumerable.Repeat((byte)0xCC, 64).ToArray();
        a.WriteBytes(dest);

        CollectionAssert.AreEqual(sol32, dest.Take(Address.SolanaByteLength).ToArray());
        Assert.IsTrue(dest.Skip(Address.SolanaByteLength).All(b => b == 0xCC));
    }

    [TestMethod]
    public void WriteBytes_DestinationTooSmall_Throws()
    {
        Address evm = Address.Parse("0x" + new string('1', 40));
        Address sol = Address.ZeroSolana;

        Assert.Throws<ArgumentException>(() => evm.WriteBytes(new byte[Address.EvmByteLength - 1]));
        Assert.Throws<ArgumentException>(() => sol.WriteBytes(new byte[Address.SolanaByteLength - 1]));
    }

    // -------------------------
    // Parsing (chars)
    // -------------------------

    [TestMethod]
    public void TryParse_Chars_Evm_With_0x_Or_0X_Prefix()
    {
        const string lower = "0x52908400098527886e0f7030069857d2e4169ee7";
        const string upperPrefix = "0X52908400098527886E0F7030069857D2E4169EE7";

        Assert.IsTrue(Address.TryParse(lower, out Address a));
        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual(lower, a.ToString());

        Assert.IsTrue(Address.TryParse(upperPrefix, out Address b));
        Assert.AreEqual(AddressType.Evm, b.Type);
        Assert.AreEqual(lower, b.ToString()); // canonical output is lower + 0x
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void TryParse_Chars_Evm_WithoutPrefix_40Hex_Fallback()
    {
        // Contains '0' so Base58 attempt should fail; then 40-hex fallback should succeed (EVM).
        string hex40 = "000102030405060708090a0b0c0d0e0f10111213";

        Assert.IsTrue(Address.TryParse(hex40, out Address a));
        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual("0x" + hex40, a.ToString());
    }

    [TestMethod]
    public void TryParse_Chars_Solana_Base58_Zero()
    {
        string s = new string('1', 32);

        Assert.IsTrue(Address.TryParse(s, out Address a));
        Assert.AreEqual(AddressType.Solana, a.Type);
        Assert.AreEqual(Address.ZeroSolana, a);
    }

    [TestMethod]
    public void TryParse_Chars_Solana_NonAscii_Fails()
    {
        // Base58 alphabet is ASCII; implementation explicitly rejects > 0x7F.
        string s = new string('1', 31) + "é";

        Assert.IsFalse(Address.TryParse(s, out Address a));
        Assert.AreEqual(default, a);
    }

    [TestMethod]
    public void TryParse_Chars_Empty_And_Null_Fail()
    {
        Assert.IsFalse(Address.TryParse(ReadOnlySpan<char>.Empty, out Address a));
        Assert.AreEqual(default, a);

        Assert.IsFalse(Address.TryParse((string?)null, out Address b));
        Assert.AreEqual(default, b);
    }

    [TestMethod]
    public void Parse_Chars_Invalid_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => Address.Parse("not-an-address"));
        Assert.Throws<FormatException>(() => Address.Parse("0x" + new string('z', 40)));
        Assert.Throws<FormatException>(() => Address.Parse(""));
    }

    // -------------------------
    // Parsing (UTF-8, quoted/unquoted)
    // -------------------------

    [TestMethod]
    public void TryParse_Utf8_Unquoted_And_Quoted_Evm()
    {
        const string s = "0x52908400098527886e0f7030069857d2e4169ee7";

        byte[] unquoted = Encoding.ASCII.GetBytes(s);
        Assert.IsTrue(Address.TryParse(unquoted, out Address a));
        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual(s, a.ToString());

        byte[] quoted = Encoding.ASCII.GetBytes("\"" + s + "\"");
        Assert.IsTrue(Address.TryParse(quoted, out Address b));
        Assert.AreEqual(AddressType.Evm, b.Type);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void TryParse_Utf8_Unquoted_And_Quoted_Solana()
    {
        Address sol = Address.FromSolanaBytes(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        string s = sol.ToString();

        byte[] unquoted = Encoding.ASCII.GetBytes(s);
        Assert.IsTrue(Address.TryParse(unquoted, out Address a));
        Assert.AreEqual(AddressType.Solana, a.Type);
        Assert.AreEqual(sol, a);

        byte[] quoted = Encoding.ASCII.GetBytes("\"" + s + "\"");
        Assert.IsTrue(Address.TryParse(quoted, out Address b));
        Assert.AreEqual(AddressType.Solana, b.Type);
        Assert.AreEqual(sol, b);
    }

    [TestMethod]
    public void TryParse_Utf8_MalformedQuotes_Fails()
    {
        byte[] bad1 = Encoding.ASCII.GetBytes("\"0x" + new string('0', 40));     // missing trailing quote
        byte[] bad2 = Encoding.ASCII.GetBytes("0x" + new string('0', 40) + "\""); // missing leading quote

        Assert.IsFalse(Address.TryParse(bad1, out _));
        Assert.IsFalse(Address.TryParse(bad2, out _));
    }

    [TestMethod]
    public void TryParse_Utf8_Empty_Fails()
    {
        Assert.IsFalse(Address.TryParse(ReadOnlySpan<byte>.Empty, out Address a));
        Assert.AreEqual(default, a);
    }

    // -------------------------
    // Formatting (EVM formats, Solana ignores format)
    // -------------------------

    [TestMethod]
    public void ToString_Evm_Format_Variants()
    {
        Address a = Address.Parse("0x52908400098527886e0f7030069857d2e4169ee7");

        Assert.AreEqual("0x52908400098527886e0f7030069857d2e4169ee7", a.ToString());
        Assert.AreEqual("0x52908400098527886e0f7030069857d2e4169ee7", a.ToString("0x", CultureInfo.InvariantCulture));
        Assert.AreEqual("0X52908400098527886E0F7030069857D2E4169EE7", a.ToString("0X", CultureInfo.InvariantCulture));
        Assert.AreEqual("52908400098527886e0f7030069857d2e4169ee7", a.ToString("x", CultureInfo.InvariantCulture));
        Assert.AreEqual("52908400098527886E0F7030069857D2E4169EE7", a.ToString("X", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_Chars_DestinationTooSmall_Fails()
    {
        Address evm = Address.Parse("0x" + new string('1', 40));
        Span<char> tooSmall = stackalloc char[Address.EvmHexLength + 1];

        bool ok = evm.TryFormat(tooSmall, out int written);
        Assert.IsFalse(ok);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryFormat_Utf8_DestinationTooSmall_Fails()
    {
        Address evm = Address.Parse("0x" + new string('1', 40));
        Span<byte> tooSmall = stackalloc byte[Address.EvmHexLength + 1];

        bool ok = evm.TryFormat(tooSmall, out int written);
        Assert.IsFalse(ok);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ToString_InvalidFormat_Evm_Throws_FormatException()
    {
        Address a = Address.Parse("0x" + new string('1', 40));
        Assert.Throws<FormatException>(() => a.ToString("z", CultureInfo.InvariantCulture));
        Assert.Throws<FormatException>(() => a.ToString("0x0", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_InvalidFormat_Solana_Ignores_Format()
    {
        Address a = Address.FromSolanaBytes(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

        string s1 = a.ToString();
        string s2 = a.ToString("z", CultureInfo.InvariantCulture); // ignored for Solana
        Assert.AreEqual(s1, s2);
    }

    // -------------------------
    // EIP-55 checksum (EVM only)
    // -------------------------

    [TestMethod]
    public void ToChecksumString_Matches_EIP55_TestVectors()
    {
        // ERC-55 test cases (EIP-55 / ERC-55):
        // - 0x529084... is a case where the checksummed output is ALL CAPS.
        Address a1 = Address.Parse("0x52908400098527886e0f7030069857d2e4169ee7");
        Assert.AreEqual("0x52908400098527886E0F7030069857D2E4169EE7", a1.ToChecksumString());
        Assert.AreEqual("0x52908400098527886E0F7030069857D2E4169EE7", a1.ToString("c", CultureInfo.InvariantCulture));
        Assert.AreEqual("0x52908400098527886E0F7030069857D2E4169EE7", a1.ToString("C", CultureInfo.InvariantCulture));

        // - Normal mixed-case output.
        Address a2 = Address.Parse("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");
        Assert.AreEqual("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", a2.ToChecksumString());
    }

    [TestMethod]
    public void ToChecksumString_NonEvm_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Address.ZeroSolana.ToChecksumString());
    }

    // -------------------------
    // Equality, comparison, hashing
    // -------------------------

    [TestMethod]
    public void Equality_And_HashCode_Agree_For_Equal_Values()
    {
        Address a = Address.Parse("0x" + new string('0', 39) + "1");
        Address b = Address.Parse("0x" + new string('0', 39) + "1");
        Address c = Address.Parse("0x" + new string('0', 39) + "2");

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);

        Assert.IsFalse(a.Equals(c));
        Assert.IsTrue(a != c);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void CompareTo_Orders_By_Type_Then_Lexicographic()
    {
        Address evm = Address.Parse("0x" + new string('0', 39) + "1");
        Address sol = Address.ZeroSolana;

        Assert.IsTrue(evm < sol);
        Assert.IsTrue(sol > evm);
        Assert.IsLessThan(0, evm.CompareTo(sol));

        // Within EVM: lexicographic big-endian compare.
        Address a = Address.Parse("0x" + new string('0', 39) + "1");
        Address b = Address.Parse("0x" + new string('0', 39) + "2");
        Assert.IsTrue(a < b);
        Assert.IsLessThan(0, a.CompareTo(b));

        // Within Solana: compare backing 32 bytes.
        Address s1 = Address.FromSolanaBytes(Enumerable.Repeat((byte)0x00, 31).Concat(new byte[] { 0x01 }).ToArray());
        Address s2 = Address.FromSolanaBytes(Enumerable.Repeat((byte)0x00, 31).Concat(new byte[] { 0x02 }).ToArray());
        Assert.IsTrue(s1 < s2);
    }

    [TestMethod]
    public void CompareTo_Object_WrongType_Throws()
    {
        Address a = Address.ZeroEvm;
        Assert.Throws<ArgumentException>(() => a.CompareTo(new object()));
    }

    // -------------------------
    // ISpanParsable surface
    // -------------------------

    [TestMethod]
    public void ISpanParsable_Overloads_Work_And_Ignore_Provider()
    {
        string s = "0x52908400098527886e0f7030069857d2e4169ee7";

        Address a = Address.Parse(s.AsSpan(), provider: CultureInfo.GetCultureInfo("tr-TR"));
        Address b = Address.Parse(s, provider: CultureInfo.GetCultureInfo("tr-TR"));
        Assert.AreEqual(a, b);

        Assert.IsTrue(Address.TryParse(s.AsSpan(), provider: CultureInfo.GetCultureInfo("tr-TR"), out Address c));
        Assert.AreEqual(a, c);

        Assert.IsTrue(Address.TryParse(s, provider: CultureInfo.GetCultureInfo("tr-TR"), out Address d));
        Assert.AreEqual(a, d);

        Assert.IsFalse(Address.TryParse((string?)null, provider: CultureInfo.InvariantCulture, out Address e));
        Assert.AreEqual(default, e);
    }

    // -------------------------
    // Helpers
    // -------------------------

    private static byte[] HexToBytes(string hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        if ((hex.Length & 1) != 0)
            throw new ArgumentException("Hex string must have an even length.", nameof(hex));

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0, j = 0; i < bytes.Length; i++, j += 2)
        {
            int hi = HexNibble(hex[j]);
            int lo = HexNibble(hex[j + 1]);
            bytes[i] = (byte)((hi << 4) | lo);
        }

        return bytes;

        static int HexNibble(char c)
        {
            if ((uint)(c - '0') <= 9u) return c - '0';
            c = (char)(c | 0x20);
            if ((uint)(c - 'a') <= 5u) return (c - 'a') + 10;
            throw new FormatException($"Invalid hex character '{c}'.");
        }
    }

    [TestMethod]
    public void ZeroEvm_And_ZeroSolana_AreNotEqual_But_Both_IsZero()
    {
        Address evm = Address.ZeroEvm;
        Address sol = Address.ZeroSolana;

        Assert.IsTrue(evm.IsZero);
        Assert.IsTrue(sol.IsZero);

        Assert.AreNotEqual(evm, sol);
        Assert.IsTrue(evm != sol);

        // Type participates in equality and hashing.
        Assert.AreNotEqual(evm.GetHashCode(), sol.GetHashCode());
    }

    [TestMethod]
    public void ToBytes_Length_Matches_Type()
    {
        Address evm = Address.Parse("0x" + new string('0', 39) + "1");
        Address sol = Address.FromSolanaBytes(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

        Assert.HasCount(Address.EvmByteLength, evm.ToBytes());
        Assert.HasCount(Address.SolanaByteLength, sol.ToBytes());
    }

    [TestMethod]
    public void WriteBytes_ExactLength_Buffers_Succeed_And_AreCorrect()
    {
        byte[] evm20 = HexToBytes("000102030405060708090a0b0c0d0e0f10111213");
        Address evm = Address.FromEvmBytes(evm20);

        byte[] evmDest = new byte[Address.EvmByteLength];
        evm.WriteBytes(evmDest);
        CollectionAssert.AreEqual(evm20, evmDest);

        byte[] sol32 = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();
        Address sol = Address.FromSolanaBytes(sol32);

        byte[] solDest = new byte[Address.SolanaByteLength];
        sol.WriteBytes(solDest);
        CollectionAssert.AreEqual(sol32, solDest);
    }

    [TestMethod]
    public void TryParse_Chars_Evm_Unprefixed_AllZeros_Uses_Evm_Fallback()
    {
        // Base58 rejects '0', so this should not be classified as Solana.
        string hex40 = new string('0', 40);

        Assert.IsTrue(Address.TryParse(hex40, out Address a));
        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual(Address.ZeroEvm, a);
        Assert.AreEqual("0x" + hex40, a.ToString());
    }

    [TestMethod]
    public void TryParse_Chars_Evm_Prefix_LengthMismatch_Fails()
    {
        Assert.IsFalse(Address.TryParse("0x" + new string('0', 39), out _)); // 41 total
        Assert.IsFalse(Address.TryParse("0x" + new string('0', 41), out _)); // 43 total
        Assert.IsFalse(Address.TryParse("0x", out _));                       // 2 total
    }

    [TestMethod]
    public void TryParse_Chars_Evm_Prefix_InvalidHex_Fails()
    {
        // Length is correct (42) but contains non-hex.
        string s = "0x" + new string('0', 39) + "g";
        Assert.IsFalse(Address.TryParse(s, out _));
    }

    [TestMethod]
    public void TryParse_Chars_DoesNotTrim_Whitespace_Fails()
    {
        string evm = "0x" + new string('1', 40);

        Assert.IsFalse(Address.TryParse(" " + evm, out _));
        Assert.IsFalse(Address.TryParse(evm + " ", out _));
        Assert.IsFalse(Address.TryParse("\n" + evm, out _));
        Assert.IsFalse(Address.TryParse(evm + "\r", out _));
    }

    [TestMethod]
    public void TryParse_Utf8_DoesNotAccept_WhitespaceAround_JsonQuotes()
    {
        string evm = "0x" + new string('1', 40);

        byte[] leadingSpace = Encoding.ASCII.GetBytes(" \"" + evm + "\"");
        byte[] trailingSpace = Encoding.ASCII.GetBytes("\"" + evm + "\" ");

        Assert.IsFalse(Address.TryParse(leadingSpace, out _));
        Assert.IsFalse(Address.TryParse(trailingSpace, out _));
    }

    [TestMethod]
    public void TryParse_Chars_Solana_LengthOutsideRange_Fails()
    {
        Assert.IsFalse(Address.TryParse(new string('1', 31), out _));
        Assert.IsFalse(Address.TryParse(new string('1', 45), out _));
    }

    [TestMethod]
    public void TryParse_Chars_Solana_InvalidBase58Alphabet_Fails()
    {
        // Base58 excludes: '0', 'O', 'I', 'l'
        Assert.IsFalse(Address.TryParse(new string('1', 31) + "0", out _));
        Assert.IsFalse(Address.TryParse(new string('1', 31) + "O", out _));
        Assert.IsFalse(Address.TryParse(new string('1', 31) + "I", out _));
        Assert.IsFalse(Address.TryParse(new string('1', 31) + "l", out _));
    }

    [TestMethod]
    public void TryParse_Utf8_Solana_NonAsciiByte_Fails()
    {
        // 32 bytes in the plausible base58 length range, but includes a non-ASCII byte.
        byte[] utf8 = Encoding.ASCII.GetBytes(new string('1', 32));
        utf8[0] = 0xFF;

        Assert.IsFalse(Address.TryParse(utf8, out _));
    }

    [TestMethod]
    public void Solana_ToString_LengthWithinBounds_For_ExtremeBytes_And_RoundTrips()
    {
        byte[] sol32 = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        Address a = Address.FromSolanaBytes(sol32);

        string s = a.ToString();
        Assert.IsTrue(s.Length >= 32 && s.Length <= Address.MaxBase58Length);

        Address b = Address.Parse(s);
        Assert.AreEqual(AddressType.Solana, b.Type);
        Assert.AreEqual(a, b);
    }


    [TestMethod]
    public void TryFormat_Utf8_Evm_Checksum_ExactBuffer_Succeeds_And_RoundTrips()
    {
        Address a = Address.Parse("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");

        Span<byte> buf = stackalloc byte[Address.EvmHexLength + 2];
        Assert.IsTrue(a.TryFormat(buf, out int written, format: "c"));
        Assert.AreEqual(Address.EvmHexLength + 2, written);

        string checksum = Encoding.ASCII.GetString(buf.Slice(0, written));

        Assert.IsTrue(Address.TryParse(checksum, out Address parsed));
        Assert.AreEqual(a, parsed);
    }

    [TestMethod]
    public void Parse_ChecksummedString_Normalises_ToLower_On_ToString()
    {
        const string checksum = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";

        Address a = Address.Parse(checksum);
        Assert.AreEqual(AddressType.Evm, a.Type);

        // Canonical output is lower hex with "0x".
        Assert.AreEqual("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed", a.ToString());
    }

    [TestMethod]
    public void CompareTo_Object_Null_Returns_One()
    {
        Address a = Address.ZeroEvm;
        Assert.AreEqual(1, a.CompareTo((object?)null));
    }

    [TestMethod]
    public void TryParse_Utf8_Evm_UppercasePrefix_MixedCaseHex_Succeeds()
    {
        string s = "0X52908400098527886E0f7030069857D2e4169eE7";
        byte[] utf8 = Encoding.ASCII.GetBytes(s);

        Assert.IsTrue(Address.TryParse(utf8, out Address a));
        Assert.AreEqual(AddressType.Evm, a.Type);
        Assert.AreEqual("0x52908400098527886e0f7030069857d2e4169ee7", a.ToString());
    }
}