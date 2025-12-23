using EccentricWare.Web3.DataTypes.Utils;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_JsonUnquoteAndSignTests
{
    [TestMethod]
    public void TryUnquoteJsonUtf8_Behaviour()
    {
        Assert.IsTrue(ByteUtils.TryUnquoteJsonUtf8("\"abc\""u8, out var unquoted));
        Assert.AreEqual("abc", Encoding.ASCII.GetString(unquoted));

        // No quotes: unchanged, still must be non-empty
        Assert.IsTrue(ByteUtils.TryUnquoteJsonUtf8("abc"u8, out unquoted));
        Assert.AreEqual("abc", Encoding.ASCII.GetString(unquoted));

        // Empty inside quotes => empty span, returns false
        Assert.IsFalse(ByteUtils.TryUnquoteJsonUtf8("\"\""u8, out unquoted));
        Assert.IsTrue(unquoted.IsEmpty);
    }

    [TestMethod]
    public void UnquoteJsonStringUtf8_Behaviour()
    {
        Assert.AreEqual("abc", Encoding.ASCII.GetString(ByteUtils.UnquoteJsonStringUtf8("\"abc\""u8)));
        Assert.AreEqual("", Encoding.ASCII.GetString(ByteUtils.UnquoteJsonStringUtf8("\"\""u8)));
        Assert.AreEqual("abc", Encoding.ASCII.GetString(ByteUtils.UnquoteJsonStringUtf8("abc"u8)));
    }

    [TestMethod]
    public void TryTrimLeadingSign_Chars()
    {
        Assert.IsTrue(ByteUtils.TryTrimLeadingSign("+123".AsSpan(), out bool neg, out var unsignedSpan));
        Assert.IsFalse(neg);
        Assert.AreEqual("123", unsignedSpan.ToString());

        Assert.IsTrue(ByteUtils.TryTrimLeadingSign("-123".AsSpan(), out neg, out unsignedSpan));
        Assert.IsTrue(neg);
        Assert.AreEqual("123", unsignedSpan.ToString());

        Assert.IsTrue(ByteUtils.TryTrimLeadingSign("123".AsSpan(), out neg, out unsignedSpan));
        Assert.IsFalse(neg);
        Assert.AreEqual("123", unsignedSpan.ToString());

        Assert.IsFalse(ByteUtils.TryTrimLeadingSign(ReadOnlySpan<char>.Empty, out _, out _));
    }

    [TestMethod]
    public void TryTrimLeadingSign_Utf8()
    {
        Assert.IsTrue(ByteUtils.TryTrimLeadingSignUtf8("+123"u8, out bool neg, out var unsignedSpan));
        Assert.IsFalse(neg);
        Assert.AreEqual("123", Encoding.ASCII.GetString(unsignedSpan));

        Assert.IsTrue(ByteUtils.TryTrimLeadingSignUtf8("-123"u8, out neg, out unsignedSpan));
        Assert.IsTrue(neg);
        Assert.AreEqual("123", Encoding.ASCII.GetString(unsignedSpan));

        Assert.IsTrue(ByteUtils.TryTrimLeadingSignUtf8("123"u8, out neg, out unsignedSpan));
        Assert.IsFalse(neg);
        Assert.AreEqual("123", Encoding.ASCII.GetString(unsignedSpan));

        Assert.IsFalse(ByteUtils.TryTrimLeadingSignUtf8(ReadOnlySpan<byte>.Empty, out _, out _));
    }

    [TestMethod]
    public void TryNormaliseHexTokenUtf8_CoversExpectedForms()
    {
        // null literal
        Assert.IsTrue(ByteUtils.TryNormaliseHexTokenUtf8(" null "u8, out bool isNull, out bool isNeg, out var digits));
        Assert.IsTrue(isNull);
        Assert.IsFalse(isNeg);
        Assert.IsTrue(digits.IsEmpty);

        // quoted null is NOT treated as null (checked before unquote); should be rejected as non-hex
        Assert.IsFalse(ByteUtils.TryNormaliseHexTokenUtf8("\"null\""u8, out _, out _, out _));

        // whitespace + quotes + 0x
        Assert.IsTrue(ByteUtils.TryNormaliseHexTokenUtf8("  \"0x0a\" "u8, out isNull, out isNeg, out digits));
        Assert.IsFalse(isNull);
        Assert.IsFalse(isNeg);
        Assert.AreEqual("0a", Encoding.ASCII.GetString(digits));

        // negative quantity
        Assert.IsTrue(ByteUtils.TryNormaliseHexTokenUtf8("-0xFf"u8, out isNull, out isNeg, out digits));
        Assert.IsFalse(isNull);
        Assert.IsTrue(isNeg);
        Assert.AreEqual("Ff", Encoding.ASCII.GetString(digits));

        // empty after sign => treated as zero (empty digits)
        Assert.IsTrue(ByteUtils.TryNormaliseHexTokenUtf8("-"u8, out isNull, out isNeg, out digits));
        Assert.IsFalse(isNull);
        Assert.IsTrue(isNeg);
        Assert.IsTrue(digits.IsEmpty);

        // empty after 0x => zero (empty digits)
        Assert.IsTrue(ByteUtils.TryNormaliseHexTokenUtf8("0x"u8, out isNull, out isNeg, out digits));
        Assert.IsFalse(isNull);
        Assert.IsFalse(isNeg);
        Assert.IsTrue(digits.IsEmpty);

        // invalid character
        Assert.IsFalse(ByteUtils.TryNormaliseHexTokenUtf8("0x0g"u8, out _, out _, out _));

        // empty token after trimming
        Assert.IsFalse(ByteUtils.TryNormaliseHexTokenUtf8("   "u8, out _, out _, out _));
    }
}