using EccentricWare.Web3.DataTypes.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_WhitespaceAndPrefixTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("abc", "abc")]
    [DataRow(" abc", "abc")]
    [DataRow("abc ", "abc")]
    [DataRow("\tabc\r\n", "abc")]
    [DataRow(" \t\r\n ", "")]
    [DataRow(" a b ", "a b")] // internal whitespace preserved
    public void TrimAsciiWhitespace_Chars(string input, string expected)
    {
        ReadOnlySpan<char> s = input.AsSpan();
        ReadOnlySpan<char> trimmed = ByteUtils.TrimAsciiWhitespace(s);
        Assert.AreEqual(expected, trimmed.ToString());
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("abc", "abc")]
    [DataRow(" abc", "abc")]
    [DataRow("abc ", "abc")]
    [DataRow("\tabc\r\n", "abc")]
    [DataRow(" \t\r\n ", "")]
    [DataRow(" a b ", "a b")]
    public void TrimAsciiWhitespace_Utf8(string input, string expected)
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes(input);
        ReadOnlySpan<byte> trimmed = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(trimmed));
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("\u00A0abc\u00A0", "abc")] // NBSP is Unicode whitespace
    [DataRow(" \tabc\n", "abc")]       // ASCII whitespace too
    public void TrimWhitespace_Unicode(string input, string expected)
    {
        var trimmed = ByteUtils.TrimWhitespace(input.AsSpan());
        Assert.AreEqual(expected, trimmed.ToString());
    }

    [TestMethod]
    public void TryTrimHexPrefix_Chars_RemovesPrefix()
    {
        Assert.IsTrue(ByteUtils.TryTrimHexPrefix("0xdead".AsSpan(), out var digits));
        Assert.AreEqual("dead", digits.ToString());

        Assert.IsTrue(ByteUtils.TryTrimHexPrefix("0XDEAD".AsSpan(), out digits));
        Assert.AreEqual("DEAD", digits.ToString());
    }

    [TestMethod]
    public void TryTrimHexPrefix_Chars_NoPrefix_SetsDefault()
    {
        Assert.IsFalse(ByteUtils.TryTrimHexPrefix("dead".AsSpan(), out var digits));
        Assert.IsTrue(digits.IsEmpty); // important contract in this implementation
    }

    [TestMethod]
    public void TryTrimHexPrefix_Utf8_RemovesPrefix()
    {
        Assert.IsTrue(ByteUtils.TryTrimHexPrefixUtf8("0xdead"u8, out var digits));
        Assert.AreEqual("dead", Encoding.ASCII.GetString(digits));

        Assert.IsTrue(ByteUtils.TryTrimHexPrefixUtf8("0XDEAD"u8, out digits));
        Assert.AreEqual("DEAD", Encoding.ASCII.GetString(digits));
    }

    [TestMethod]
    public void TryTrimHexPrefix_Utf8_NoPrefix_SetsDefault()
    {
        Assert.IsFalse(ByteUtils.TryTrimHexPrefixUtf8("dead"u8, out var digits));
        Assert.IsTrue(digits.IsEmpty);
    }

    [TestMethod]
    public void AsSpanSafe_NullAndNonNull()
    {
        string? s = null;
        Assert.IsTrue(ByteUtils.AsSpanSafe(s).IsEmpty);

        s = "abc";
        Assert.AreEqual("abc", ByteUtils.AsSpanSafe(s).ToString());
    }
}