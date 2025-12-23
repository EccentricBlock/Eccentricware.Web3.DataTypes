using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class Hash32Tests
{
    [TestMethod]
    public void Constants_AreExpected()
    {
        Assert.AreEqual(32, Hash32.ByteLength);
        Assert.AreEqual(64, Hash32.HexLength);
    }

    [TestMethod]
    public void Zero_IsZero_AndEqualsDefault()
    {
        Hash32 z1 = Hash32.Zero;
        Hash32 z2 = default;

        Assert.IsTrue(z1.IsZero);
        Assert.IsTrue(z2.IsZero);
        Assert.IsTrue(z1 == z2);
        Assert.IsFalse(z1 != z2);

        string s = z1.ToString();
        Assert.AreEqual(66, s.Length);
        Assert.IsTrue(s.StartsWith("0x", StringComparison.Ordinal));
        Assert.IsTrue(s.Skip(2).All(c => c == '0'));
    }

    [TestMethod]
    public void Ctor_Limbs_WriteBigEndian_RoundTripsBytes()
    {
        // Arrange: pick limbs that exercise all nibbles.
        var value = new Hash32(
            0x0102030405060708UL,
            0x1112131415161718UL,
            0xA1A2A3A4A5A6A7A8UL,
            0xFFEEDDCCBBAA9988UL);

        Span<byte> be = stackalloc byte[Hash32.ByteLength];
        value.WriteBigEndian(be);

        // Act
        var roundTrip = new Hash32(be);

        // Assert
        Assert.AreEqual(value, roundTrip);

        // Validate exact byte order for the first and last limb.
        CollectionAssert.AreEqual(
            new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
            be.Slice(0, 8).ToArray());

        CollectionAssert.AreEqual(
            new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88 },
            be.Slice(24, 8).ToArray());
    }

    [TestMethod]
    public void Ctor_BigEndianBytes_InvalidLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => _ = new Hash32(ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentException>(() => _ = new Hash32(new byte[31]));
        Assert.Throws<ArgumentException>(() => _ = new Hash32(new byte[33]));
    }

    [TestMethod]
    public void FromLittleEndian_RoundTrips_WithWriteLittleEndian()
    {
        byte[] bytes = CreateBytes0To31();
        var bigEndianValue = new Hash32(bytes);

        Span<byte> le = stackalloc byte[Hash32.ByteLength];
        bigEndianValue.WriteLittleEndian(le);

        var fromLe = Hash32.FromLittleEndian(le);

        Assert.AreEqual(bigEndianValue, fromLe);

        // Also check the inverse:
        Span<byte> be2 = stackalloc byte[Hash32.ByteLength];
        fromLe.WriteBigEndian(be2);
        CollectionAssert.AreEqual(bytes, be2.ToArray());
    }

    [TestMethod]
    public void WriteBigEndian_DestinationTooSmall_Throws()
    {
        var value = new Hash32(CreateBytes0To31());
        Assert.Throws<ArgumentException>(() => value.WriteBigEndian(new byte[31]));
    }

    [TestMethod]
    public void WriteLittleEndian_DestinationTooSmall_Throws()
    {
        var value = new Hash32(CreateBytes0To31());
        Assert.Throws<ArgumentException>(() => value.WriteLittleEndian(new byte[31]));
    }

    [TestMethod]
    public void ToBigEndianBytes_And_ToLittleEndianBytes_AreCorrect()
    {
        byte[] be = CreateBytes0To31();
        var value = new Hash32(be);

        byte[] be2 = value.ToBigEndianBytes();
        Assert.HasCount(Hash32.ByteLength, be2);
        CollectionAssert.AreEqual(be, be2);

        byte[] le = value.ToLittleEndianBytes();
        Assert.HasCount(Hash32.ByteLength, le);

        // little-endian output should be be reversed by significance (byte-level reverse)
        CollectionAssert.AreEqual(be.Reverse().ToArray(), le);

        // FromLittleEndian should reconstruct original
        var fromLe = Hash32.FromLittleEndian(le);
        Assert.AreEqual(value, fromLe);
    }

    [TestMethod]
    public void Equality_Operators_And_GetHashCode()
    {
        var a = new Hash32(CreateBytes0To31());
        var b = new Hash32(CreateBytes0To31());

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

        // Make a different value
        byte[] bytes = CreateBytes0To31();
        bytes[31] ^= 0x01;
        var c = new Hash32(bytes);

        Assert.IsFalse(a.Equals(c));
        Assert.IsTrue(a != c);
    }

    [TestMethod]
    public void CompareTo_Lexicographic_ByMostSignificantFirst()
    {
        var a = new Hash32(1, 0, 0, 0);
        var b = new Hash32(2, 0, 0, 0);
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= b);
        Assert.IsTrue(b >= a);
        Assert.AreEqual(-1, a.CompareTo(b));
        Assert.AreEqual(1, b.CompareTo(a));
        Assert.AreEqual(0, a.CompareTo(new Hash32(1, 0, 0, 0)));

        // Difference in least significant limb only.
        var c = new Hash32(0, 0, 0, 1);
        var d = new Hash32(0, 0, 0, 2);
        Assert.IsTrue(c < d);
    }

    [TestMethod]
    public void CompareTo_Object_NullAndWrongType()
    {
        var value = new Hash32(CreateBytes0To31());

        Assert.IsGreaterThan(0, value.CompareTo(null));
        Assert.Throws<ArgumentException>(() => value.CompareTo("not a hash"));
    }

    [TestMethod]
    public void Parse_TryParse_Hex_CharSpan_WithPrefixAndWhitespace()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string hexLower = ToHex(bytes, uppercase: false);
        string hexUpper = ToHex(bytes, uppercase: true);

        // Without prefix
        Assert.IsTrue(Hash32.TryParse(hexLower.AsSpan(), out var p1));
        Assert.AreEqual(expected, p1);

        // With prefix + whitespace
        string s2 = " \t0x" + hexLower + "\r\n";
        Assert.IsTrue(Hash32.TryParse(s2.AsSpan(), out var p2));
        Assert.AreEqual(expected, p2);

        // With 0X uppercase prefix + uppercase digits
        string s3 = "0X" + hexUpper;
        Assert.IsTrue(Hash32.TryParse(s3.AsSpan(), out var p3));
        Assert.AreEqual(expected, p3);

        // Parse(...) should match TryParse for valid cases
        Assert.AreEqual(expected, Hash32.Parse(hexLower.AsSpan()));
        Assert.AreEqual(expected, Hash32.Parse(("0x" + hexLower).AsSpan()));
    }

    [TestMethod]
    public void TryParse_String_NullOrEmpty_False_AndResultZero()
    {
        Assert.IsFalse(Hash32.TryParse((string?)null, out var r1));
        Assert.IsTrue(r1.IsZero);

        Assert.IsFalse(Hash32.TryParse(string.Empty, out var r2));
        Assert.IsTrue(r2.IsZero);
    }

    [TestMethod]
    public void TryParse_Hex_InvalidLengthOrInvalidChars_ReturnsFalse()
    {
        Assert.IsFalse(Hash32.TryParse("0x".AsSpan(), out _));
        Assert.IsFalse(Hash32.TryParse(new string('a', 63).AsSpan(), out _));
        Assert.IsFalse(Hash32.TryParse(new string('a', 65).AsSpan(), out _));

        // Non-hex character
        string bad = new string('a', 63) + "g";
        Assert.IsFalse(Hash32.TryParse(bad.AsSpan(), out _));
    }

    [TestMethod]
    public void Parse_Hex_Invalid_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Hash32.Parse("0x".AsSpan()));
        Assert.Throws<FormatException>(() => Hash32.Parse((new string('a', 63) + "g").AsSpan()));
    }

    [TestMethod]
    public void TryParseHexUtf8_Works_WithPrefixAndWhitespace()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string hex = ToHex(bytes, uppercase: false);
        byte[] utf8 = Encoding.ASCII.GetBytes(" \n0x" + hex + "\t");

        Assert.IsTrue(Hash32.TryParseHexUtf8(utf8, out var parsed));
        Assert.AreEqual(expected, parsed);

        // Parse(ReadOnlySpan<byte>) uses TryParseHexUtf8 internally
        Assert.AreEqual(expected, Hash32.Parse(utf8));
    }

    [TestMethod]
    public void TryParseHexUtf8_Invalid_ReturnsFalse()
    {
        Assert.IsFalse(Hash32.TryParseHexUtf8(ReadOnlySpan<byte>.Empty, out _));
        Assert.IsFalse(Hash32.TryParseHexUtf8(Encoding.ASCII.GetBytes("0x" + new string('a', 63)), out _));
        Assert.IsFalse(Hash32.TryParseHexUtf8(Encoding.ASCII.GetBytes("0x" + new string('a', 63) + "g"), out _));
    }

    [TestMethod]
    public void Parse_Utf8Hex_Invalid_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Hash32.Parse(Encoding.ASCII.GetBytes("0x" + new string('a', 63) + "g")));
    }

    [TestMethod]
    public void ParseBase58_Zero32_Works_AndInvalidFails()
    {
        // Canonical Base58 encoding of 32 zero bytes is 32 '1' characters.
        byte[] b58Zero = Encoding.ASCII.GetBytes(new string('1', 32));

        var h = Hash32.ParseBase58(b58Zero);
        Assert.IsTrue(h.IsZero);

        Assert.IsTrue(Hash32.TryParseBase58(b58Zero, out var h2));
        Assert.IsTrue(h2.IsZero);

        // Invalid Base58 (contains '0')
        byte[] bad = Encoding.ASCII.GetBytes("10");
        Assert.IsFalse(Hash32.TryParseBase58(bad, out _));
        Assert.Throws<FormatException>(() => Hash32.ParseBase58(bad));
    }

    [TestMethod]
    public void ParseBase64_StandardAndUrlSafe_Works()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string b64 = Convert.ToBase64String(bytes);
        byte[] utf8 = Encoding.ASCII.GetBytes(b64);

        Assert.IsTrue(Hash32.TryParseBase64(utf8, out var p1));
        Assert.AreEqual(expected, p1);
        Assert.AreEqual(expected, Hash32.ParseBase64(utf8));

        // URL-safe Base64 variant (keep padding; replace chars)
        string b64Url = b64.Replace('+', '-').Replace('/', '_');
        byte[] utf8Url = Encoding.ASCII.GetBytes(b64Url);

        Assert.IsTrue(Hash32.TryParseBase64(utf8Url, out var p2));
        Assert.AreEqual(expected, p2);
        Assert.AreEqual(expected, Hash32.ParseBase64(utf8Url));
    }

    [TestMethod]
    public void TryParseBase64_WrongDecodedLength_ReturnsFalse()
    {
        // 31 bytes => base64 decodes to 31; must be rejected.
        byte[] bytes31 = Enumerable.Range(0, 31).Select(i => (byte)i).ToArray();
        string b64 = Convert.ToBase64String(bytes31);

        Assert.IsFalse(Hash32.TryParseBase64(Encoding.ASCII.GetBytes(b64), out _));
    }

    [TestMethod]
    public void TryParseAuto_PrefersHex_WhenPrefixedOrAllHex_ElseFallsBack()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string hex = ToHex(bytes, uppercase: false);
        byte[] prefixedHex = Encoding.ASCII.GetBytes("0x" + hex);
        Assert.IsTrue(Hash32.TryParseAuto(prefixedHex, out var p1));
        Assert.AreEqual(expected, p1);

        byte[] bareHex = Encoding.ASCII.GetBytes(hex);
        Assert.IsTrue(Hash32.TryParseAuto(bareHex, out var p2));
        Assert.AreEqual(expected, p2);

        // Base58 (zero)
        byte[] b58Zero = Encoding.ASCII.GetBytes(new string('1', 32));
        Assert.IsTrue(Hash32.TryParseAuto(b58Zero, out var p3));
        Assert.IsTrue(p3.IsZero);

        // Base64 fallback case (should fail base58 then succeed base64)
        string b64 = Convert.ToBase64String(bytes);
        Assert.IsTrue(Hash32.TryParseAuto(Encoding.ASCII.GetBytes(b64), out var p4));
        Assert.AreEqual(expected, p4);
    }

    [TestMethod]
    public void ToString_Default_And_FormatVariants()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string hexLower = ToHex(bytes, uppercase: false);
        string hexUpper = ToHex(bytes, uppercase: true);

        // Default is 0x + lowercase
        Assert.AreEqual("0x" + hexLower, value.ToString());

        // "x" => lowercase no prefix
        Assert.AreEqual(hexLower, value.ToString("x", CultureInfo.InvariantCulture));

        // "X" => uppercase no prefix
        Assert.AreEqual(hexUpper, value.ToString("X", CultureInfo.InvariantCulture));

        // "0x" => lowercase with prefix
        Assert.AreEqual("0x" + hexLower, value.ToString("0x", CultureInfo.InvariantCulture));

        // "0X" => uppercase digits with prefix (prefix is still "0x" per implementation)
        Assert.AreEqual("0x" + hexUpper, value.ToString("0X", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToString_InvalidFormat_ThrowsFormatException()
    {
        var value = new Hash32(CreateBytes0To31());
        Assert.Throws<FormatException>(() => value.ToString("G", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_Chars_WritesExpected_AndFailsWhenTooSmall()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string expected = "0x" + ToHex(bytes, uppercase: false);

        Span<char> tooSmall = stackalloc char[65];
        Assert.IsFalse(value.TryFormat(tooSmall, out int writtenSmall));
        Assert.AreEqual(0, writtenSmall);

        Span<char> dst = stackalloc char[66];
        Assert.IsTrue(value.TryFormat(dst, out int written));
        Assert.AreEqual(66, written);
        Assert.AreEqual(expected, new string(dst));
    }

    [TestMethod]
    public void TryFormat_Utf8_WritesExpected_AndFailsWhenTooSmall()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string expected = "0x" + ToHex(bytes, uppercase: false);

        Span<byte> tooSmall = stackalloc byte[65];
        Assert.IsFalse(value.TryFormat(tooSmall, out int writtenSmall));
        Assert.AreEqual(0, writtenSmall);

        Span<byte> dst = stackalloc byte[66];
        Assert.IsTrue(value.TryFormat(dst, out int written));
        Assert.AreEqual(66, written);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(dst));
    }

    [TestMethod]
    public void IndexOf_And_CountEquals_Work()
    {
        var a = new Hash32(0, 0, 0, 1);
        var b = new Hash32(0, 0, 0, 2);
        var c = new Hash32(0, 0, 0, 3);

        // Ensure length >= 8 to allow SIMD branch if available, but correctness must hold either way.
        Hash32[] hay = new[] { a, b, c, b, a, b, c, a, a, c };

        Assert.AreEqual(1, Hash32.IndexOf(hay, b));
        Assert.AreEqual(0, Hash32.IndexOf(hay, a));
        Assert.AreEqual(2, Hash32.IndexOf(hay, c));

        var missing = new Hash32(0, 0, 0, 999);
        Assert.AreEqual(-1, Hash32.IndexOf(hay, missing));

        Assert.AreEqual(4, Hash32.CountEquals(hay, a));
        Assert.AreEqual(3, Hash32.CountEquals(hay, b));
        Assert.AreEqual(3, Hash32.CountEquals(hay, c));
        Assert.AreEqual(0, Hash32.CountEquals(Array.Empty<Hash32>(), a));
    }

    [TestMethod]
    public void ExplicitConversion_FromByteArray_Works_AndInvalidLengthThrows()
    {
        byte[] bytes = CreateBytes0To31();
        Hash32 h = (Hash32)bytes;
        Assert.AreEqual(new Hash32(bytes), h);

        Assert.Throws<ArgumentException>(() => _ = (Hash32)new byte[31]);
    }

    [TestMethod]
    public void UInt256_Conversions_RoundTrip()
    {
        // Assumes uint256 exists in the referenced main assembly (as per your codebase).
        var original = new Hash32(CreateBytes0To31());
        uint256 asU = original.ToUInt256();
        var roundTrip = Hash32.FromUInt256(asU);

        Assert.AreEqual(original, roundTrip);
    }

    // ----------------- helpers -----------------

    private static byte[] CreateBytes0To31()
    {
        var bytes = new byte[32];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)i;
        return bytes;
    }

    private static string ToHex(byte[] bytes, bool uppercase)
    {
        const string lower = "0123456789abcdef";
        const string upper = "0123456789ABCDEF";
        string table = uppercase ? upper : lower;

        char[] chars = new char[bytes.Length * 2];
        int j = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[j++] = table[b >> 4];
            chars[j++] = table[b & 0x0F];
        }
        return new string(chars);
    }

    [TestMethod]
    public void IsZero_False_WhenAnyLimbNonZero()
    {
        Assert.IsFalse(new Hash32(0, 0, 0, 1).IsZero);
        Assert.IsFalse(new Hash32(0, 0, 1, 0).IsZero);
        Assert.IsFalse(new Hash32(0, 1, 0, 0).IsZero);
        Assert.IsFalse(new Hash32(1, 0, 0, 0).IsZero);
    }

    [TestMethod]
    public void TryParse_CharSpan_DoesNotAllowInteriorWhitespace()
    {
        byte[] bytes = CreateBytes0To31();
        string hex = ToHex(bytes, uppercase: false);

        // Insert a space into the middle -> should fail (only leading/trailing whitespace is trimmed).
        string bad = "0x" + hex.Substring(0, 10) + " " + hex.Substring(10);

        Assert.IsFalse(Hash32.TryParse(bad.AsSpan(), out var parsed));
        Assert.IsTrue(parsed.IsZero);
    }

    [TestMethod]
    public void TryParseHexUtf8_Rejects_NonAsciiWhitespace_NotTrimmed()
    {
        byte[] bytes = CreateBytes0To31();
        string hex = ToHex(bytes, uppercase: false);

        // NBSP (U+00A0) is not ASCII whitespace; TrimAsciiWhitespaceUtf8 should not remove it.
        string withNbsp = "\u00A00x" + hex + "\u00A0";
        byte[] utf8 = Encoding.UTF8.GetBytes(withNbsp);

        Assert.IsFalse(Hash32.TryParseHexUtf8(utf8, out _));
    }

    [TestMethod]
    public void TryParseBase58_TrimsAsciiWhitespace()
    {
        // Base58(32 zero bytes) == "11111111111111111111111111111111"
        string s = " \t\r\n" + new string('1', 32) + "\n";
        byte[] utf8 = Encoding.ASCII.GetBytes(s);

        Assert.IsTrue(Hash32.TryParseBase58(utf8, out var parsed));
        Assert.IsTrue(parsed.IsZero);
    }

    [TestMethod]
    public void TryParseBase64_TrimsAsciiWhitespace()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string b64 = Convert.ToBase64String(bytes);
        byte[] utf8 = Encoding.ASCII.GetBytes("\r\n\t " + b64 + "  \n");

        Assert.IsTrue(Hash32.TryParseBase64(utf8, out var parsed));
        Assert.AreEqual(expected, parsed);
    }

    [TestMethod]
    public void TryParseAuto_0xPrefix_ForcesHexOnly_NoFallback()
    {
        // Starts with 0x but is not valid 64-hex => MUST fail and not attempt base58/base64.
        byte[] utf8 = Encoding.ASCII.GetBytes("0x" + new string('1', 32)); // looks like base58 but is hex-forced

        Assert.IsFalse(Hash32.TryParseAuto(utf8, out var parsed));
        Assert.IsTrue(parsed.IsZero);
    }

    [TestMethod]
    public void Parse_Utf8Hex_UppercaseDigits_Works()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string hexUpper = ToHex(bytes, uppercase: true);
        byte[] utf8 = Encoding.ASCII.GetBytes("0X" + hexUpper);

        Assert.AreEqual(expected, Hash32.Parse(utf8));
    }

    [TestMethod]
    public void Parse_WithIFormatProvider_IsInvariant()
    {
        byte[] bytes = CreateBytes0To31();
        var expected = new Hash32(bytes);

        string hex = "0x" + ToHex(bytes, uppercase: false);

        // Provider should be ignored.
        Assert.AreEqual(expected, Hash32.Parse(hex.AsSpan(), new CultureInfo("tr-TR")));
        Assert.IsTrue(Hash32.TryParse(hex.AsSpan(), new CultureInfo("ar-SA"), out var parsed));
        Assert.AreEqual(expected, parsed);
    }

    [TestMethod]
    public void ToString_EmptyFormat_IsDefault()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string expected = "0x" + ToHex(bytes, uppercase: false);

        Assert.AreEqual(expected, value.ToString(string.Empty, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryFormat_Chars_RespectsFormatFlags_AndPrefixBehaviour()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string lower = ToHex(bytes, uppercase: false);
        string upper = ToHex(bytes, uppercase: true);

        // "x" => lowercase no prefix
        Span<char> dst64 = stackalloc char[64];
        Assert.IsTrue(value.TryFormat(dst64, out int written64, "x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(64, written64);
        Assert.AreEqual(lower, new string(dst64));

        // "X" => uppercase no prefix
        Assert.IsTrue(value.TryFormat(dst64, out written64, "X".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(64, written64);
        Assert.AreEqual(upper, new string(dst64));

        // "0X" => uppercase digits with prefix; implementation writes prefix as "0x"
        Span<char> dst66 = stackalloc char[66];
        Assert.IsTrue(value.TryFormat(dst66, out int written66, "0X".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(66, written66);
        Assert.AreEqual("0x" + upper, new string(dst66));
    }

    [TestMethod]
    public void TryFormat_Utf8_RespectsFormatFlags_AndPrefixBehaviour()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        string lower = ToHex(bytes, uppercase: false);
        string upper = ToHex(bytes, uppercase: true);

        // "x" => lowercase no prefix
        Span<byte> dst64 = stackalloc byte[64];
        Assert.IsTrue(value.TryFormat(dst64, out int written64, "x".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(64, written64);
        Assert.AreEqual(lower, Encoding.ASCII.GetString(dst64));

        // "X" => uppercase no prefix
        Assert.IsTrue(value.TryFormat(dst64, out written64, "X".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(64, written64);
        Assert.AreEqual(upper, Encoding.ASCII.GetString(dst64));

        // "0X" => uppercase digits with prefix; implementation writes prefix as "0x"
        Span<byte> dst66 = stackalloc byte[66];
        Assert.IsTrue(value.TryFormat(dst66, out int written66, "0X".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual(66, written66);
        Assert.AreEqual("0x" + upper, Encoding.ASCII.GetString(dst66));
    }

    [TestMethod]
    public void IndexOf_Empty_ReturnsMinus1()
    {
        Assert.AreEqual(-1, Hash32.IndexOf(ReadOnlySpan<Hash32>.Empty, Hash32.Zero));
    }

    [TestMethod]
    public void Evm_TxHash_Hex_With0xPrefix_MixedCase_NormalisesToLowercase()
    {
        // EVM-style: 0x + 64 hex chars (case-insensitive on input).
        byte[] bytes = CreateBytes0To31();
        string lower = ToHex(bytes, uppercase: false);
        string upper = ToHex(bytes, uppercase: true);

        // Mix casing deliberately.
        string mixed = "0x" + upper.Substring(0, 20) + lower.Substring(20);

        var parsed = Hash32.Parse(mixed);

        // Normalisation: ToString() is always "0x" + lowercase.
        Assert.AreEqual("0x" + lower, parsed.ToString());

        // Round-trip via hex without prefix.
        Assert.IsTrue(Hash32.TryParse(parsed.ToString("x", CultureInfo.InvariantCulture).AsSpan(), out var parsed2));
        Assert.AreEqual(parsed, parsed2);
    }

    [TestMethod]
    public void Evm_TryParseAuto_Bare64Hex_TreatedAsHex()
    {
        // EVM nodes and systems sometimes emit 64-hex without a prefix in internal pipelines.
        byte[] bytes = CreateBytes0To31();
        string hex64 = ToHex(bytes, uppercase: false);

        byte[] utf8 = Encoding.ASCII.GetBytes(hex64);

        Assert.IsTrue(Hash32.TryParseAuto(utf8, out var autoParsed));
        Assert.AreEqual("0x" + hex64, autoParsed.ToString());

        // Parse(char span) should match the same value.
        Assert.AreEqual(Hash32.Parse(hex64.AsSpan()), autoParsed);
    }

    [TestMethod]
    public void Evm_WriteBigEndian_ProducesHexThatMatchesEvmNormalisedString()
    {
        byte[] bytes = CreateBytes0To31();
        var value = new Hash32(bytes);

        Span<byte> be = stackalloc byte[Hash32.ByteLength];
        value.WriteBigEndian(be);

        string hexFromBytes = ToHex(be.ToArray(), uppercase: false);
        Assert.AreEqual(value.ToString("x", CultureInfo.InvariantCulture), hexFromBytes);
        Assert.AreEqual("0x" + hexFromBytes, value.ToString());
    }

    [TestMethod]
    public void Solana_SystemProgram_Base58_AllOnes_ParsesToZero_AndAutoChoosesBase58()
    {
        // Solana System Program ID is "11111111111111111111111111111111" which is also Base58(32 zero bytes).
        string systemProgram = new string('1', 32);
        byte[] utf8 = Encoding.ASCII.GetBytes(systemProgram);

        Assert.IsTrue(Hash32.TryParseBase58(utf8, out var b58));
        Assert.IsTrue(b58.IsZero);

        Assert.IsTrue(Hash32.TryParseAuto(utf8, out var autoParsed));
        Assert.AreEqual(b58, autoParsed);

        // Cross-chain normalisation: ToString() is still EVM-style 0x + 64 zeros.
        Assert.AreEqual(Hash32.Zero.ToString(), autoParsed.ToString());
    }

    [TestMethod]
    public void Solana_SysvarClock_Base58_Parses_And_CanBeNormalisedToHexAndBack()
    {
        // A canonical Solana Sysvar pubkey (Base58, 32-byte decoded payload).
        const string sysvarClock = "SysvarC1ock11111111111111111111111111111111";
        byte[] utf8 = Encoding.ASCII.GetBytes(sysvarClock);

        Assert.IsTrue(Hash32.TryParseBase58(utf8, out var parsedB58));
        Assert.IsFalse(parsedB58.IsZero);

        Assert.IsTrue(Hash32.TryParseAuto(utf8, out var parsedAuto));
        Assert.AreEqual(parsedB58, parsedAuto);

        // Normalise to hex (no prefix), then parse as EVM-style hex => same value.
        string hex64 = parsedB58.ToString("x", CultureInfo.InvariantCulture);
        Assert.AreEqual(Hash32.HexLength, hex64.Length);
        Assert.IsTrue(hex64.All(Uri.IsHexDigit));

        Assert.IsTrue(Hash32.TryParse(hex64.AsSpan(), out var parsedHex));
        Assert.AreEqual(parsedB58, parsedHex);
    }

    [TestMethod]
    public void Solana_Base58_ShouldNotParseAsHexUtf8()
    {
        const string sysvarClock = "SysvarC1ock11111111111111111111111111111111";
        byte[] utf8 = Encoding.ASCII.GetBytes(sysvarClock);

        Assert.IsFalse(Hash32.TryParseHexUtf8(utf8, out var parsedHex));
        Assert.IsTrue(parsedHex.IsZero);
    }

}
