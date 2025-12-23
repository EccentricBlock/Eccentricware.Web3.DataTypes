using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace EccentricWare.Web3.DataTypes.Tests;


[TestClass]
public sealed class FunctionSelector_EvmTests
{
    [TestMethod]
    public void Size_Is4Bytes()
    {
        Assert.AreEqual(4, Unsafe.SizeOf<FunctionSelector>());
        Assert.AreEqual(4, Marshal.SizeOf<FunctionSelector>());
    }

    [TestMethod]
    public void Zero_IsDefault_AndFormatsAsCanonicalHex()
    {
        Assert.AreEqual(default, FunctionSelector.Zero);
        Assert.IsTrue(FunctionSelector.Zero.IsZero);
        Assert.AreEqual("0x00000000", FunctionSelector.Zero.ToString());
        Assert.AreEqual("00000000", FunctionSelector.Zero.ToString("x"));
        Assert.AreEqual("00000000", FunctionSelector.Zero.ToString("X"));
        Assert.AreEqual("0x00000000", FunctionSelector.Zero.ToString("0x"));
        Assert.AreEqual("0X00000000", FunctionSelector.Zero.ToString("0X"));
    }

    [TestMethod]
    public void Ctor_FromUIntValue_PreservesValue_AndCanonicalString()
    {
        var s = new FunctionSelector(0xa9059cbb);
        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.IsFalse(s.IsZero);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void Ctor_FromBytes_ReadsBigEndian()
    {
        ReadOnlySpan<byte> bytes = new byte[] { 0xA9, 0x05, 0x9C, 0xBB };
        var s = new FunctionSelector(bytes);

        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void Ctor_FromBytes_TooShort_Throws()
    {
        byte[] bytes = new byte[] { 0xA9, 0x05, 0x9C };
        Assert.Throws<ArgumentException>(() => _ = new FunctionSelector(bytes));
    }

    [TestMethod]
    public void Ctor_FromFourBytes_BigEndianComposition()
    {
        var s = new FunctionSelector(0xA9, 0x05, 0x9C, 0xBB);
        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void WriteBytes_WritesBigEndian()
    {
        var s = new FunctionSelector(0xa9059cbb);
        Span<byte> dst = stackalloc byte[4];
        s.WriteBytes(dst);

        CollectionAssert.AreEqual(new byte[] { 0xA9, 0x05, 0x9C, 0xBB }, dst.ToArray());
    }

    [TestMethod]
    public void WriteBytes_DestinationTooSmall_Throws()
    {
        var s = new FunctionSelector(0xa9059cbb);
        byte[] dst = new byte[3];
        Assert.Throws<ArgumentException>(() => s.WriteBytes(dst.AsSpan()));
    }

    [TestMethod]
    public void FromCalldata_ExtractsFirst4Bytes()
    {
        byte[] calldata = { 0xA9, 0x05, 0x9C, 0xBB, 0x00, 0x01, 0x02 };
        var s = FunctionSelector.FromCalldata(calldata);

        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void FromCalldata_TooShort_Throws()
    {
        byte[] calldata = { 0xA9, 0x05, 0x9C };
        Assert.Throws<ArgumentException>(() => _ = FunctionSelector.FromCalldata(calldata));
    }

    [TestMethod]
    public void TryFromCalldata_TooShort_ReturnsFalse_AndZero()
    {
        byte[] calldata = { 0xA9, 0x05, 0x9C };
        bool ok = FunctionSelector.TryFromCalldata(calldata, out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
        Assert.IsTrue(s.IsZero);
    }

    [TestMethod]
    public void TryFromCalldata_Valid_ReturnsTrue()
    {
        byte[] calldata = { 0xA9, 0x05, 0x9C, 0xBB, 0xFF };
        bool ok = FunctionSelector.TryFromCalldata(calldata, out var s);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xa9059cbbu, s.Value);
    }

    [TestMethod]
    [DataRow("a9059cbb", 0xa9059cbb)]
    [DataRow("0xa9059cbb", 0xa9059cbb)]
    [DataRow("0Xa9059cbb", 0xa9059cbb)]
    [DataRow("A9059CBB", 0xa9059cbb)]
    [DataRow("0xA9059CBB", 0xa9059cbb)]
    [DataRow(" \t\r\n0xa9059cbb \n", 0xa9059cbb)]
    public void TryParse_Chars_AcceptsPrefixCaseAndWhitespace(string input, uint expected)
    {
        bool ok = FunctionSelector.TryParse(input.AsSpan(), out var s);

        Assert.IsTrue(ok);
        Assert.AreEqual(expected, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0x")]
    [DataRow("0xa9059c")]
    [DataRow("0xa9059cbb00")]
    [DataRow("0xa9059cbg")] // invalid hex digit
    [DataRow("0xzzzzzzzz")]
    [DataRow("not-hex")]
    public void TryParse_Chars_Invalid_ReturnsFalse(string input)
    {
        bool ok = FunctionSelector.TryParse(input.AsSpan(), out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void Parse_Chars_Invalid_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => _ = FunctionSelector.Parse("0xa9059cbg".AsSpan()));
    }

    [TestMethod]
    public void Parse_Utf8_Invalid_ThrowsFormatException()
    {
        byte[] utf8 = Encoding.ASCII.GetBytes("0xa9059cbg");
        Assert.Throws<FormatException>(() => _ = FunctionSelector.Parse(utf8));
    }

    [TestMethod]
    public void TryParse_Utf8_AcceptsPrefixCaseAndWhitespace()
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes(" \n\t0Xa9059cbb\r ");
        bool ok = FunctionSelector.TryParse(utf8, out var s);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xa9059cbbu, s.Value);
    }

    [TestMethod]
    public void TryParseFromCalldataHexUtf8_ParsesOnlyFirst8Digits_AllowsExtraData()
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes(" 0xa9059cbb000000000000000000000000deadbeef ");
        bool ok = FunctionSelector.TryParseFromCalldataHexUtf8(utf8, out var s);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xa9059cbbu, s.Value);
    }

    [TestMethod]
    public void TryParseFromCalldataHexUtf8_TooShort_ReturnsFalse()
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes("0xa9059c");
        bool ok = FunctionSelector.TryParseFromCalldataHexUtf8(utf8, out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void TryParseFromCalldataHexUtf8_InvalidFirst8Digits_ReturnsFalse()
    {
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes("0xa9059cbg00000000");
        bool ok = FunctionSelector.TryParseFromCalldataHexUtf8(utf8, out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void ToString_FormatsSupportedVariants_AndRejectsUnknown()
    {
        var s = new FunctionSelector(0x12ab34cdu);

        Assert.AreEqual("0x12ab34cd", s.ToString());          // canonical
        Assert.AreEqual("12ab34cd", s.ToString("x"));         // no prefix, lower
        Assert.AreEqual("12AB34CD", s.ToString("X"));         // no prefix, upper
        Assert.AreEqual("0x12ab34cd", s.ToString("0x"));      // prefix, lower
        Assert.AreEqual("0X12AB34CD", s.ToString("0X"));      // prefix, upper

        Assert.Throws<FormatException>(() => _ = s.ToString("G"));
        Assert.Throws<FormatException>(() => _ = s.ToString("0x0"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("x")]
    [DataRow("X")]
    [DataRow("0x")]
    [DataRow("0X")]
    public void TryFormat_Chars_ProducesExpectedOutput(string? fmt)
    {
        var s = new FunctionSelector(0xa9059cbb);

        Span<char> buf = stackalloc char[16];
        bool ok = s.TryFormat(buf, out int written, fmt is null ? default : fmt.AsSpan());

        Assert.IsTrue(ok);

        string actual = new string(buf.Slice(0, written));
        string expected = fmt switch
        {
            null or "" => "0xa9059cbb",
            "x" => "a9059cbb",
            "X" => "A9059CBB",
            "0x" => "0xa9059cbb",
            "0X" => "0XA9059CBB",
            _ => throw new InvalidOperationException("Test case mismatch.")
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryFormat_Chars_DestinationTooSmall_ReturnsFalse()
    {
        var s = new FunctionSelector(0xa9059cbb);

        Span<char> buf = stackalloc char[7]; // needs 8 without prefix; 10 with prefix (default)
        bool ok = s.TryFormat(buf, out int written);

        Assert.IsFalse(ok);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("x")]
    [DataRow("X")]
    [DataRow("0x")]
    [DataRow("0X")]
    public void TryFormat_Utf8_ProducesExpectedOutput(string? fmt)
    {
        var s = new FunctionSelector(0xa9059cbb);

        Span<byte> buf = stackalloc byte[16];
        bool ok = s.TryFormat(buf, out int written, fmt is null ? default : fmt.AsSpan());

        Assert.IsTrue(ok);

        string actual = Encoding.ASCII.GetString(buf.Slice(0, written));
        string expected = fmt switch
        {
            null or "" => "0xa9059cbb",
            "x" => "a9059cbb",
            "X" => "A9059CBB",
            "0x" => "0xa9059cbb",
            "0X" => "0XA9059CBB",
            _ => throw new InvalidOperationException("Test case mismatch.")
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryFormat_Utf8_DestinationTooSmall_ReturnsFalse()
    {
        var s = new FunctionSelector(0xa9059cbb);

        Span<byte> buf = stackalloc byte[8]; // needs 10 by default
        bool ok = s.TryFormat(buf, out int written);

        Assert.IsFalse(ok);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void FromInt32_RoundTripsSignedValues()
    {
        var s = FunctionSelector.FromInt32(-1);
        Assert.AreEqual(0xffffffffu, s.Value);
        Assert.AreEqual(-1, s.AsInt32);
        Assert.AreEqual("0xffffffff", s.ToString());
    }

    [TestMethod]
    public void ExplicitCasts_Work()
    {
        var s = new FunctionSelector(0x01020304u);

        Assert.AreEqual(0x01020304u, (uint)s);
        Assert.AreEqual(unchecked((int)0x01020304u), (int)s);

        var s2 = (FunctionSelector)0x0a0b0c0du;
        Assert.AreEqual("0x0a0b0c0d", s2.ToString());
    }

    [TestMethod]
    public void EqualityHashingAndOrdering_BehaveAsExpected()
    {
        var a = new FunctionSelector(0x00000001u);
        var b = new FunctionSelector(0x00000002u);
        var a2 = new FunctionSelector(0x00000001u);

        Assert.IsTrue(a == a2);
        Assert.IsTrue(a.Equals(a2));
        Assert.IsTrue(a.Equals((object)a2));
        Assert.IsFalse(a.Equals((object)"not a selector"));

        Assert.AreEqual(a.GetHashCode(), a2.GetHashCode());
        Assert.IsTrue(a != b);
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= a2);
        Assert.IsTrue(b >= a2);

        Assert.AreEqual(0, a.CompareTo(a2));
        Assert.IsLessThan(0, a.CompareTo(b));
        Assert.IsGreaterThan(0, b.CompareTo(a));

        Assert.AreEqual(1, a.CompareTo((object?)null));
        Assert.Throws<ArgumentException>(() => _ = a.CompareTo((object)"wrong-type"));
    }

    [TestMethod]
    public void Ordering_MatchesHexOrdering()
    {
        // Ensures the design invariant: numeric ordering matches hex string ordering.
        var lo = new FunctionSelector(0x00ffffffu);
        var hi = new FunctionSelector(0x01000000u);

        Assert.IsTrue(lo < hi);
        Assert.IsLessThan(0, string.CompareOrdinal(lo.ToString("x"), hi.ToString("x")));
    }

    [TestMethod]
    public void FromSignature_KnownErc20Selectors_MatchConstants()
    {
        Assert.AreEqual(FunctionSelector.Erc20.Transfer, FunctionSelector.FromSignature("transfer(address,uint256)"));
        Assert.AreEqual(FunctionSelector.Erc20.Approve, FunctionSelector.FromSignature("approve(address,uint256)"));
        Assert.AreEqual(FunctionSelector.Erc20.TransferFrom, FunctionSelector.FromSignature("transferFrom(address,address,uint256)"));
        Assert.AreEqual(FunctionSelector.Erc20.BalanceOf, FunctionSelector.FromSignature("balanceOf(address)"));
        Assert.AreEqual(FunctionSelector.Erc20.Allowance, FunctionSelector.FromSignature("allowance(address,address)"));
        Assert.AreEqual(FunctionSelector.Erc20.TotalSupply, FunctionSelector.FromSignature("totalSupply()"));
    }

    [TestMethod]
    public void FromSignature_KnownErc721Selectors_MatchConstants()
    {
        Assert.AreEqual(FunctionSelector.Erc721.SafeTransferFrom, FunctionSelector.FromSignature("safeTransferFrom(address,address,uint256)"));
        Assert.AreEqual(FunctionSelector.Erc721.SafeTransferFromWithData, FunctionSelector.FromSignature("safeTransferFrom(address,address,uint256,bytes)"));
        Assert.AreEqual(FunctionSelector.Erc721.OwnerOf, FunctionSelector.FromSignature("ownerOf(uint256)"));
        Assert.AreEqual(FunctionSelector.Erc721.TokenUri, FunctionSelector.FromSignature("tokenURI(uint256)"));
    }

    [TestMethod]
    public void FromSignature_EmptyCharSpan_ReturnsZero()
    {
        Assert.AreEqual(FunctionSelector.Zero, FunctionSelector.FromSignature(ReadOnlySpan<char>.Empty));
        Assert.AreEqual(FunctionSelector.Zero, FunctionSelector.FromSignature(""));
    }

    [TestMethod]
    public void FromSignature_StringNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = FunctionSelector.FromSignature((string)null!));
    }

    [TestMethod]
    public void FromSignature_Overloads_AreConsistent_ForNonEmptyInput()
    {
        const string sig = "transfer(address,uint256)";

        var a = FunctionSelector.FromSignature(sig);
        var b = FunctionSelector.FromSignature(sig.AsSpan());
        var c = FunctionSelector.FromSignature(Encoding.UTF8.GetBytes(sig));

        Assert.AreEqual(a, b);
        Assert.AreEqual(a, c);
    }

    [TestMethod]
    public void FromSignature_Utf8Empty_ComputesKeccakOfEmpty_NotZero()
    {
        // Keccak-256("") = c5d2460186f7233c... (NOT SHA3-256 which starts a7ffc6f8...)
        // Selector is the first 4 bytes: 0xc5d24601.
        var s = FunctionSelector.FromSignature(ReadOnlySpan<byte>.Empty);
        Assert.AreEqual(0xc5d24601u, s.Value);
        Assert.AreEqual("0xc5d24601", s.ToString());
    }

    [TestMethod]
    public void FromSignature_LongSignature_ExercisesHeapUtf8Path_AndIsStableAcrossOverloads()
    {
        // Forces Encoding.UTF8 max byte count > 256 to take the heap path in FromSignature(ReadOnlySpan<char>).
        string longSig = "f(" + string.Join(",", Enumerable.Repeat("uint256", 200)) + ")";

        var a = FunctionSelector.FromSignature(longSig.AsSpan());
        var b = FunctionSelector.FromSignature(longSig);
        var c = FunctionSelector.FromSignature(Encoding.UTF8.GetBytes(longSig));

        Assert.AreEqual(a, b);
        Assert.AreEqual(a, c);
        Assert.IsFalse(a.IsZero); // overwhelmingly likely; still a correctness check (not a cryptographic guarantee)
    }

    [TestMethod]
    public void BinarySearchSorted_FindsAndNotFinds()
    {
        var sorted = new[]
        {
            new FunctionSelector(0x00000001u),
            new FunctionSelector(0x00000010u),
            new FunctionSelector(0x00ffffffu),
            new FunctionSelector(0x01000000u),
            new FunctionSelector(0xffffffffu),
        };

        Assert.AreEqual(0, FunctionSelector.BinarySearchSorted(sorted, new FunctionSelector(0x00000001u)));
        Assert.AreEqual(2, FunctionSelector.BinarySearchSorted(sorted, new FunctionSelector(0x00ffffffu)));
        Assert.AreEqual(4, FunctionSelector.BinarySearchSorted(sorted, new FunctionSelector(0xffffffffu)));

        Assert.AreEqual(-1, FunctionSelector.BinarySearchSorted(sorted, new FunctionSelector(0x00000002u)));
        Assert.AreEqual(-1, FunctionSelector.BinarySearchSorted(ReadOnlySpan<FunctionSelector>.Empty, new FunctionSelector(0x00000001u)));
    }

    [TestMethod]
    public void IsInSortedAllowList_UsesBinarySearchSemantics()
    {
        var allow = new[]
        {
            FunctionSelector.Erc20.Approve,
            FunctionSelector.Erc20.BalanceOf,
            FunctionSelector.Erc20.Transfer,
            FunctionSelector.Erc20.TransferFrom,
        }.OrderBy(s => s.Value).ToArray();

        Assert.IsTrue(FunctionSelector.IsInSortedAllowList(allow, FunctionSelector.Erc20.Transfer));
        Assert.IsFalse(FunctionSelector.IsInSortedAllowList(allow, FunctionSelector.Erc20.TotalSupply));
    }

    [TestMethod]
    public void ContainsSimd_FindsAndNotFinds_ForSmallAndLargeSpans()
    {
        var target = new FunctionSelector(0x11223344u);

        // Small (scalar fallback)
        var small = new[]
        {
            new FunctionSelector(0x00000000u),
            new FunctionSelector(0x01020304u),
            target,
            new FunctionSelector(0xffffffffu),
        };
        Assert.IsTrue(FunctionSelector.ContainsSimd(small, target));
        Assert.IsFalse(FunctionSelector.ContainsSimd(small, new FunctionSelector(0x55667788u)));

        // Large (may SIMD-accelerate depending on CPU/JIT)
        var large = Enumerable.Range(0, 256).Select(i => new FunctionSelector((uint)i)).ToArray();
        large[200] = target;

        Assert.IsTrue(FunctionSelector.ContainsSimd(large, target));
        Assert.IsFalse(FunctionSelector.ContainsSimd(large, new FunctionSelector(0xDEADBEEFu)));
    }

    [TestMethod]
    public void TryParse_StringNull_ReturnsFalseAndZero()
    {
        Assert.IsFalse(FunctionSelector.TryParse((string?)null, out var s));
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void ISpanParsable_ParseAndTryParse_Semantics_Work()
    {
        var s = FunctionSelector.Parse("0xa9059cbb", provider: null);
        Assert.AreEqual(FunctionSelector.Erc20.Transfer, s);

        bool ok = FunctionSelector.TryParse("0xa9059cbb", provider: null, out var t);
        Assert.IsTrue(ok);
        Assert.AreEqual(FunctionSelector.Erc20.Transfer, t);

        bool bad = FunctionSelector.TryParse("0xa9059cbg", provider: null, out var u);
        Assert.IsFalse(bad);
        Assert.AreEqual(FunctionSelector.Zero, u);

        Assert.Throws<FormatException>(() => _ = FunctionSelector.Parse("0xa9059cbg", provider: null));
    }

    [TestMethod]
    public void Parse_String_UsesSpanParsablePath()
    {
        var s = FunctionSelector.Parse("0xa9059cbb");
        Assert.AreEqual(FunctionSelector.Erc20.Transfer, s);
    }

    [TestMethod]
    public void ToString_FormatNull_DefaultsTo0xLowercase()
    {
        var s = new FunctionSelector(0xA9059CBBu);

        string formatted = s.ToString(format: null, formatProvider: null);

        Assert.AreEqual("0xa9059cbb", formatted);
    }

    [TestMethod]
    public void ToString_PadsToFixed8HexDigits()
    {
        var s = new FunctionSelector(1u);

        Assert.AreEqual("0x00000001", s.ToString());
        Assert.AreEqual("00000001", s.ToString("x"));
        Assert.AreEqual("00000001", s.ToString("X"));
    }

    [TestMethod]
    public void TryParse_DoesNotTrimNonAsciiWhitespace_NbspIsRejected()
    {
        // NBSP is not ASCII whitespace; TrimAsciiWhitespace should not remove it.
        string input = "\u00A0" + "0xa9059cbb" + "\u00A0";

        bool ok = FunctionSelector.TryParse(input.AsSpan(), out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void TryParse_InternalWhitespace_Fails()
    {
        bool ok = FunctionSelector.TryParse("0xa905 9cbb".AsSpan(), out var s);

        Assert.IsFalse(ok);
        Assert.AreEqual(FunctionSelector.Zero, s);
    }

    [TestMethod]
    public void Parse_Chars_AllowsLeadingTrailingAsciiWhitespace()
    {
        var s = FunctionSelector.Parse(" \r\n\t0xA9059CBB \n".AsSpan());

        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void TryParseFromCalldataHexUtf8_IgnoresTrailingGarbageAfterFirst8Digits()
    {
        // The API contract: only reads first 8 hex digits after optional 0x; input may contain additional data.
        // Trailing non-hex should not matter.
        ReadOnlySpan<byte> utf8 = Encoding.ASCII.GetBytes("0xa9059cbbGARBAGE_NOT_HEX");
        bool ok = FunctionSelector.TryParseFromCalldataHexUtf8(utf8, out var s);

        Assert.IsTrue(ok);
        Assert.AreEqual(0xa9059cbbu, s.Value);
    }

    [TestMethod]
    public void Ctor_FromByteSpan_ReadsFirst4Bytes_IgnoresTail()
    {
        byte[] bytes = { 0xA9, 0x05, 0x9C, 0xBB, 0xFF, 0xEE, 0xDD };
        var s = new FunctionSelector(bytes);

        Assert.AreEqual(0xa9059cbbu, s.Value);
        Assert.AreEqual("0xa9059cbb", s.ToString());
    }

    [TestMethod]
    public void TryFormat_Chars_UnknownFormat_ThrowsFormatException()
    {
        var s = new FunctionSelector(0x12345678u);
        char[] buf = new char[16];

        Assert.Throws<FormatException>(() => _ = s.TryFormat(buf.AsSpan(), out _, "G".AsSpan()));
    }

    [TestMethod]
    public void TryFormat_Utf8_UnknownFormat_ThrowsFormatException()
    {
        var s = new FunctionSelector(0x12345678u);
        byte[] buf = new byte[16];

        Assert.Throws<FormatException>(() => _ = s.TryFormat(buf.AsSpan(), out _, "G".AsSpan()));
    }

    [TestMethod]
    public void ContainsSimd_EmptyHaystack_ReturnsFalse()
    {
        bool ok = FunctionSelector.ContainsSimd(ReadOnlySpan<FunctionSelector>.Empty, new FunctionSelector(0x01020304u));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void ContainsSimd_FindsZeroSelector()
    {
        var haystack = new[]
        {
            new FunctionSelector(0x01020304u),
            FunctionSelector.Zero,
            new FunctionSelector(0xffffffffu),
        };

        Assert.IsTrue(FunctionSelector.ContainsSimd(haystack, FunctionSelector.Zero));
    }

    [TestMethod]
    public void BinarySearchSorted_SingleElement_Works()
    {
        var one = new[] { new FunctionSelector(0x01020304u) };

        Assert.AreEqual(0, FunctionSelector.BinarySearchSorted(one, new FunctionSelector(0x01020304u)));
        Assert.AreEqual(-1, FunctionSelector.BinarySearchSorted(one, new FunctionSelector(0x01020305u)));
    }

    [TestMethod]
    public void IsInSortedAllowList_Empty_ReturnsFalse()
    {
        Assert.IsFalse(FunctionSelector.IsInSortedAllowList(ReadOnlySpan<FunctionSelector>.Empty, FunctionSelector.Erc20.Transfer));
    }

    [TestMethod]
    public void CompareTo_ObjectWrongType_ThrowsArgumentException()
    {
        var s = FunctionSelector.Erc20.Transfer;
        Assert.Throws<ArgumentException>(() => _ = s.CompareTo((object)123));
    }

    [TestMethod]
    public void Parsing_IsCaseInsensitive_ButRequiresExact8DigitsAfterPrefix()
    {
        Assert.IsTrue(FunctionSelector.TryParse("0xA9059CBB".AsSpan(), out var ok1));
        Assert.AreEqual(0xa9059cbbu, ok1.Value);

        Assert.IsFalse(FunctionSelector.TryParse("0xA9059CBB00".AsSpan(), out _)); // too long
        Assert.IsFalse(FunctionSelector.TryParse("0xA9059CB".AsSpan(), out _));   // too short
    }

    [TestMethod]
    public void ToString_UppercasePrefix_UppercaseDigits()
    {
        var s = new FunctionSelector(0xa9059cbb);

        Assert.AreEqual("0XA9059CBB", s.ToString("0X"));
    }

    [TestMethod]
    public void Evm_CommonSelectors_ToStringMatchesExpected()
    {
        Assert.AreEqual("0xa9059cbb", FunctionSelector.Erc20.Transfer.ToString());
        Assert.AreEqual("0x095ea7b3", FunctionSelector.Erc20.Approve.ToString());
        Assert.AreEqual("0x23b872dd", FunctionSelector.Erc20.TransferFrom.ToString());
        Assert.AreEqual("0x70a08231", FunctionSelector.Erc20.BalanceOf.ToString());
        Assert.AreEqual("0xdd62ed3e", FunctionSelector.Erc20.Allowance.ToString());
        Assert.AreEqual("0x18160ddd", FunctionSelector.Erc20.TotalSupply.ToString());
    }

    [TestMethod]
    public void Sorting_ByValue_MatchesSorting_ByHexString_NoPrefix()
    {
        var items = new[]
        {
            new FunctionSelector(0x00ffffffu),
            new FunctionSelector(0x00000001u),
            new FunctionSelector(0x01000000u),
            new FunctionSelector(0x0000ffffu),
        };

        var byValue = items.OrderBy(x => x.Value).Select(x => x.ToString("x")).ToArray();
        var byString = items.Select(x => x.ToString("x")).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(byString, byValue);
    }
}