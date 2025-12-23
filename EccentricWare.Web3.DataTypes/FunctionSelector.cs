using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;
/// <summary>
/// Represents a 4-byte EVM function selector (the first 4 bytes of Keccak-256(functionSignature)).
/// Stored as a single big-endian <see cref="uint"/> for cache density, fast comparisons, and predictable ordering.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 4)]
[JsonConverter(typeof(FunctionSelectorJsonConverter))]
public readonly struct FunctionSelector :
    IEquatable<FunctionSelector>,
    IComparable<FunctionSelector>,
    IComparable,
    ISpanFormattable,
    ISpanParsable<FunctionSelector>,
    IUtf8SpanFormattable
{
    /// <summary>Selector length in bytes.</summary>
    public const int ByteLength = 4;

    /// <summary>Selector length in hex digits (without 0x prefix).</summary>
    public const int HexLength = 8;

    /// <summary>Represents the zero selector (0x00000000).</summary>
    public static readonly FunctionSelector Zero;

    /// <summary>
    /// Stored as big-endian numeric value so that numeric ordering matches hex string ordering.
    /// </summary>
    private readonly uint _value;

    /// <summary>
    /// Creates a selector from a big-endian numeric value.
    /// </summary>
    /// <param name="value">Big-endian selector value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FunctionSelector(uint value) => _value = value;

    /// <summary>
    /// Creates a selector from a 4-byte big-endian span.
    /// </summary>
    /// <param name="bytes">Input bytes (must be at least 4 bytes).</param>
    public FunctionSelector(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidFunctionSelectorLength(nameof(bytes));

        _value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    /// <summary>
    /// Creates a selector from four bytes (big-endian order).
    /// </summary>
    /// <param name="b0">Most significant byte.</param>
    /// <param name="b1">Second byte.</param>
    /// <param name="b2">Third byte.</param>
    /// <param name="b3">Least significant byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FunctionSelector(byte b0, byte b1, byte b2, byte b3)
        => _value = ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3;

    /// <summary>
    /// Gets the selector as a big-endian numeric value.
    /// </summary>
    public uint Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Gets the selector as a signed 32-bit integer (useful for some database providers).
    /// </summary>
    public int AsInt32
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((int)_value);
    }

    /// <summary>
    /// Gets whether the selector is 0x00000000.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value == 0;
    }

    /// <summary>
    /// Writes the selector to a destination buffer as 4 bytes in big-endian order.
    /// </summary>
    /// <param name="destination">Destination span (must be at least 4 bytes).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt32BigEndian(destination, _value);
    }

    /// <summary>
    /// Creates a selector from a signed 32-bit value (useful for database round-trips).
    /// </summary>
    /// <param name="value">Signed database value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FunctionSelector FromInt32(int value) => new(unchecked((uint)value));

    /// <summary>
    /// Extracts the selector from decoded calldata bytes (first 4 bytes).
    /// </summary>
    /// <param name="calldata">Decoded calldata bytes.</param>
    public static FunctionSelector FromCalldata(ReadOnlySpan<byte> calldata)
    {
        if (calldata.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidFunctionSelectorLength(nameof(calldata));

        return new FunctionSelector(calldata);
    }

    /// <summary>
    /// Attempts to extract the selector from decoded calldata bytes (first 4 bytes) without throwing.
    /// </summary>
    /// <param name="calldata">Decoded calldata bytes.</param>
    /// <param name="selector">Parsed selector if successful.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFromCalldata(ReadOnlySpan<byte> calldata, out FunctionSelector selector)
    {
        if (calldata.Length < ByteLength)
        {
            selector = Zero;
            return false;
        }

        selector = new FunctionSelector(calldata);
        return true;
    }

    /// <summary>
    /// Computes a selector from an EVM function signature (cold path).
    /// Example: "transfer(address,uint256)" -> 0xa9059cbb.
    /// </summary>
    /// <param name="signature">Function signature string.</param>
    public static FunctionSelector FromSignature(string signature)
    {
        if (signature is null) ThrowHelper.ThrowArgumentNullException(nameof(signature));

        // Cold path: string => UTF-8 bytes.
        return FromSignature(signature.AsSpan());
    }

    /// <summary>
    /// Computes a selector from an EVM function signature (UTF-16 chars, cold path).
    /// Encodes to UTF-8 without allocating for short signatures.
    /// </summary>
    /// <param name="signatureChars">Function signature characters.</param>
    public static FunctionSelector FromSignature(ReadOnlySpan<char> signatureChars)
    {
        if (signatureChars.IsEmpty)
            return Zero;

        const int StackLimit = 256;
        int maxBytes = Encoding.UTF8.GetMaxByteCount(signatureChars.Length);

        Span<byte> utf8 = maxBytes <= StackLimit ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int written = Encoding.UTF8.GetBytes(signatureChars, utf8);

        return FromSignature(utf8.Slice(0, written));
    }

    /// <summary>
    /// Computes a selector from an UTF-8 function signature (cold path).
    /// </summary>
    /// <param name="signatureUtf8">Signature bytes in UTF-8.</param>
    public static FunctionSelector FromSignature(ReadOnlySpan<byte> signatureUtf8)
    {
        Span<byte> hash32 = stackalloc byte[32];
        Keccak256.ComputeHash(signatureUtf8, hash32);
        return new FunctionSelector(hash32.Slice(0, ByteLength));
    }

    /// <summary>
    /// Returns a canonical hex string with 0x prefix and lowercase digits.
    /// </summary>
    public override string ToString()
        => string.Create(HexLength + 2, _value, static (chars, v) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            ByteUtils.WriteHexUInt32CharsFixed8(chars.Slice(2), v, uppercase: false);
        });

    /// <summary>
    /// Formats the selector using supported formats: "", "x", "X", "0x", "0X".
    /// Empty format defaults to "0x" with lowercase digits.
    /// </summary>
    /// <param name="format">Format specifier.</param>
    /// <param name="formatProvider">Unused (present for interface compatibility).</param>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        DecodeFormat(format.AsSpanSafe(), out bool hasPrefix, out bool uppercaseDigits, out char prefixChar);

        int len = hasPrefix ? HexLength + 2 : HexLength;

        return string.Create(len, (Value: _value, HasPrefix: hasPrefix, Upper: uppercaseDigits, PrefixChar: prefixChar), static (dst, state) =>
        {
            if (state.HasPrefix)
            {
                dst[0] = '0';
                dst[1] = state.PrefixChar;
                ByteUtils.WriteHexUInt32CharsFixed8(dst.Slice(2), state.Value, state.Upper);
            }
            else
            {
                ByteUtils.WriteHexUInt32CharsFixed8(dst, state.Value, state.Upper);
            }
        });
    }

    /// <summary>
    /// Attempts to format the selector into a character buffer without allocations.
    /// Supported formats: "", "x", "X", "0x", "0X".
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        DecodeFormat(format, out bool hasPrefix, out bool uppercaseDigits, out char prefixChar);

        int required = hasPrefix ? HexLength + 2 : HexLength;
        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            destination[0] = '0';
            destination[1] = prefixChar;
            ByteUtils.WriteHexUInt32CharsFixed8(destination.Slice(2), _value, uppercaseDigits);
        }
        else
        {
            ByteUtils.WriteHexUInt32CharsFixed8(destination, _value, uppercaseDigits);
        }

        charsWritten = required;
        return true;
    }

    /// <summary>
    /// Attempts to format the selector into a UTF-8 buffer without allocations.
    /// Supported formats: "", "x", "X", "0x", "0X".
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        DecodeFormat(format, out bool hasPrefix, out bool uppercaseDigits, out char prefixChar);

        int required = hasPrefix ? HexLength + 2 : HexLength;
        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)prefixChar;
            ByteUtils.WriteHexUInt32Utf8Fixed8(utf8Destination.Slice(2), _value, uppercaseDigits);
        }
        else
        {
            ByteUtils.WriteHexUInt32Utf8Fixed8(utf8Destination, _value, uppercaseDigits);
        }

        bytesWritten = required;
        return true;
    }

    /// <summary>
    /// Parses an 8-hex-digit selector literal (with optional 0x/0X prefix).
    /// This method expects exactly 8 digits after the prefix and throws on invalid input.
    /// </summary>
    /// <param name="hex">Hex characters.</param>
    public static FunctionSelector Parse(ReadOnlySpan<char> hex)
    {
        if (!TryParse(hex, out var result))
            ThrowHelper.ThrowFormatExceptionInvalidFunctionSelector();

        return result;
    }

    /// <summary>
    /// Parses an UTF-8 selector literal (with optional 0x/0X prefix).
    /// This method expects exactly 8 digits after the prefix and throws on invalid input.
    /// </summary>
    /// <param name="utf8">UTF-8 bytes containing the hex selector.</param>
    public static FunctionSelector Parse(ReadOnlySpan<byte> utf8)
    {
        if (!TryParse(utf8, out var result))
            ThrowHelper.ThrowFormatExceptionInvalidFunctionSelector();

        return result;
    }

    /// <summary>
    /// Parses a selector using <see cref="ISpanParsable{TSelf}"/> semantics.
    /// </summary>
    public static FunctionSelector Parse(string s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a selector using <see cref="ISpanParsable{TSelf}"/> semantics.
    /// </summary>
    public static FunctionSelector Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out var result))
            return result;

        ThrowHelper.ThrowFormatExceptionInvalidFunctionSelector();
        return default;
    }

    /// <summary>
    /// Parses a selector using <see cref="ISpanParsable{TSelf}"/> semantics.
    /// </summary>
    public static FunctionSelector Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Attempts to parse a selector literal from UTF-8 without throwing.
    /// This method expects exactly 8 digits after an optional 0x prefix.
    /// </summary>
    /// <param name="utf8Hex">UTF-8 hex input.</param>
    /// <param name="result">Parsed selector if successful.</param>
    public static bool TryParse(ReadOnlySpan<byte> utf8Hex, out FunctionSelector result)
    {
        result = Zero;

        utf8Hex = ByteUtils.TrimAsciiWhitespaceUtf8(utf8Hex);

        if (utf8Hex.Length == 0)
            return false;

        if (ByteUtils.TryTrimHexPrefixUtf8(utf8Hex, out var trimmed))
            utf8Hex = trimmed;

        if (utf8Hex.Length != HexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt32Utf8Fixed8(utf8Hex, out uint value))
            return false;

        result = new FunctionSelector(value);
        return true;
    }

    /// <summary>
    /// Attempts to parse a selector literal from UTF-16 without throwing.
    /// This method expects exactly 8 digits after an optional 0x prefix.
    /// </summary>
    /// <param name="hex">Hex characters.</param>
    /// <param name="result">Parsed selector if successful.</param>
    public static bool TryParse(ReadOnlySpan<char> hex, out FunctionSelector result)
    {
        result = Zero;

        hex = ByteUtils.TrimAsciiWhitespace(hex);

        if (hex.Length == 0)
            return false;

        if (ByteUtils.TryTrimHexPrefix(hex, out var trimmed))
            hex = trimmed;

        if (hex.Length != HexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt32CharsFixed8(hex, out uint value))
            return false;

        result = new FunctionSelector(value);
        return true;
    }

    /// <summary>
    /// Attempts to parse a selector literal from a string without throwing.
    /// </summary>
    public static bool TryParse(string? hex, out FunctionSelector result)
    {
        if (hex is null)
        {
            result = Zero;
            return false;
        }

        return TryParse(hex.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a selector using <see cref="ISpanParsable{TSelf}"/> semantics.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FunctionSelector result)
        => TryParse(s, out result);

    /// <summary>
    /// Attempts to parse a selector using <see cref="ISpanParsable{TSelf}"/> semantics.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FunctionSelector result)
    {
        if (s is null)
        {
            result = Zero;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse an EVM selector from the start of a calldata hex string (UTF-8) without allocations.
    /// Reads only the first 8 hex digits after an optional 0x prefix; the input may contain additional hex data.
    /// </summary>
    /// <param name="calldataHexUtf8">UTF-8 calldata hex string.</param>
    /// <param name="selector">Parsed selector if successful.</param>
    public static bool TryParseFromCalldataHexUtf8(ReadOnlySpan<byte> calldataHexUtf8, out FunctionSelector selector)
    {
        selector = Zero;

        calldataHexUtf8 = ByteUtils.TrimAsciiWhitespaceUtf8(calldataHexUtf8);

        if (calldataHexUtf8.Length == 0)
            return false;

        if (ByteUtils.TryTrimHexPrefixUtf8(calldataHexUtf8, out var trimmed))
            calldataHexUtf8 = trimmed;

        if (calldataHexUtf8.Length < HexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt32Utf8Fixed8(calldataHexUtf8.Slice(0, HexLength), out uint value))
            return false;

        selector = new FunctionSelector(value);
        return true;
    }

    /// <summary>
    /// Performs a binary search over a sorted selector span (ascending by numeric value).
    /// </summary>
    /// <param name="sortedAllowList">Sorted selector span.</param>
    /// <param name="selector">Selector to find.</param>
    /// <returns>True if found; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInSortedAllowList(ReadOnlySpan<FunctionSelector> sortedAllowList, FunctionSelector selector)
        => BinarySearchSorted(sortedAllowList, selector) >= 0;

    /// <summary>
    /// Returns the index of a selector in a sorted span using binary search; returns -1 if not found.
    /// </summary>
    /// <param name="sorted">Sorted selector span.</param>
    /// <param name="value">Selector to find.</param>
    public static int BinarySearchSorted(ReadOnlySpan<FunctionSelector> sorted, FunctionSelector value)
    {
        // Reinterpret to uint for direct comparisons.
        ReadOnlySpan<uint> span = MemoryMarshal.Cast<FunctionSelector, uint>(sorted);

        int lo = 0;
        int hi = span.Length - 1;
        uint target = value._value;

        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            uint v = span[mid];

            if (v < target) lo = mid + 1;
            else if (v > target) hi = mid - 1;
            else return mid;
        }

        return -1;
    }

    /// <summary>
    /// Returns true if <paramref name="needle"/> is contained in <paramref name="haystack"/>.
    /// Uses SIMD for large spans when supported; falls back to scalar scanning.
    /// </summary>
    /// <param name="haystack">Selector span to scan.</param>
    /// <param name="needle">Selector to find.</param>
    public static bool ContainsSimd(ReadOnlySpan<FunctionSelector> haystack, FunctionSelector needle)
    {
        ReadOnlySpan<uint> values = MemoryMarshal.Cast<FunctionSelector, uint>(haystack);
        uint target = needle._value;

        int i = 0;

        // Threshold should be tuned under production workloads.
        if (values.Length >= 64 && Avx2.IsSupported)
        {
            Vector256<uint> vt = Vector256.Create(target);
            int last = values.Length & ~7;

            ref uint r0 = ref MemoryMarshal.GetReference(values);
            for (; i < last; i += 8)
            {
                Vector256<uint> v = Unsafe.ReadUnaligned<Vector256<uint>>(
                    ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref r0, i)));

                Vector256<uint> eq = Avx2.CompareEqual(v, vt);
                if (Avx2.MoveMask(eq.AsByte()) != 0)
                    return true;
            }
        }
        else if (values.Length >= 32 && Sse2.IsSupported)
        {
            Vector128<uint> vt = Vector128.Create(target);
            int last = values.Length & ~3;

            ref uint r0 = ref MemoryMarshal.GetReference(values);
            for (; i < last; i += 4)
            {
                Vector128<uint> v = Unsafe.ReadUnaligned<Vector128<uint>>(
                    ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref r0, i)));

                Vector128<uint> eq = Sse2.CompareEqual(v, vt);
                if (Sse2.MoveMask(eq.AsByte()) != 0)
                    return true;
            }
        }

        for (; i < values.Length; i++)
        {
            if (values[i] == target)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a selector constructed from an unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FunctionSelector(uint value) => new(value);

    /// <summary>
    /// Returns the selector value as an unsigned integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(FunctionSelector selector) => selector._value;

    /// <summary>
    /// Returns the selector value as a signed 32-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(FunctionSelector selector) => selector.AsInt32;

    /// <summary>
    /// Tests selector equality.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FunctionSelector other) => _value == other._value;

    /// <summary>
    /// Tests selector equality against an arbitrary object.
    /// </summary>
    public override bool Equals(object? obj) => obj is FunctionSelector other && Equals(other);

    /// <summary>
    /// Returns a hash code suitable for hash tables.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => unchecked((int)_value);

    /// <summary>
    /// Compares this selector to another selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FunctionSelector other) => _value.CompareTo(other._value);

    /// <summary>
    /// Compares this selector to an arbitrary object.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is FunctionSelector other) return CompareTo(other);
        ThrowHelper.ThrowArgumentExceptionWrongType(nameof(obj), nameof(FunctionSelector));
        return 0;
    }

    /// <summary>Equality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FunctionSelector left, FunctionSelector right) => left._value == right._value;

    /// <summary>Inequality operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FunctionSelector left, FunctionSelector right) => left._value != right._value;

    /// <summary>Less-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FunctionSelector left, FunctionSelector right) => left._value < right._value;

    /// <summary>Greater-than operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FunctionSelector left, FunctionSelector right) => left._value > right._value;

    /// <summary>Less-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FunctionSelector left, FunctionSelector right) => left._value <= right._value;

    /// <summary>Greater-than-or-equal operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FunctionSelector left, FunctionSelector right) => left._value >= right._value;

    /// <summary>
    /// Decodes supported format strings without allocations or expensive comparisons.
    /// Supported formats: "", "x", "X", "0x", "0X".
    /// </summary>
    /// <param name="format">Format span.</param>
    /// <param name="hasPrefix">True if output should contain 0x/0X prefix.</param>
    /// <param name="uppercaseDigits">True for uppercase A-F digits.</param>
    /// <param name="prefixChar">'x' or 'X' when <paramref name="hasPrefix"/> is true.</param>
    private static void DecodeFormat(ReadOnlySpan<char> format, out bool hasPrefix, out bool uppercaseDigits, out char prefixChar)
    {
        // Default format: 0x + lowercase digits.
        if (format.Length == 0)
        {
            hasPrefix = true;
            uppercaseDigits = false;
            prefixChar = 'x';
            return;
        }

        if (format.Length == 1)
        {
            char c = format[0];
            if (c == 'x')
            {
                hasPrefix = false;
                uppercaseDigits = false;
                prefixChar = 'x';
                return;
            }

            if (c == 'X')
            {
                hasPrefix = false;
                uppercaseDigits = true;
                prefixChar = 'X';
                return;
            }
        }
        else if (format.Length == 2 && format[0] == '0')
        {
            char c = format[1];
            if (c == 'x')
            {
                hasPrefix = true;
                uppercaseDigits = false;
                prefixChar = 'x';
                return;
            }

            if (c == 'X')
            {
                hasPrefix = true;
                uppercaseDigits = true;
                prefixChar = 'X';
                return;
            }
        }

        ThrowHelper.ThrowFormatExceptionUnknownFormat(format);
        hasPrefix = true;
        uppercaseDigits = false;
        prefixChar = 'x';
    }

    /// <summary>
    /// Common ERC-20 selectors (compile-time constants).
    /// </summary>
    public static class Erc20
    {
        /// <summary>transfer(address,uint256) = 0xa9059cbb</summary>
        public static readonly FunctionSelector Transfer = new(0xa9059cbb);

        /// <summary>approve(address,uint256) = 0x095ea7b3</summary>
        public static readonly FunctionSelector Approve = new(0x095ea7b3);

        /// <summary>transferFrom(address,address,uint256) = 0x23b872dd</summary>
        public static readonly FunctionSelector TransferFrom = new(0x23b872dd);

        /// <summary>balanceOf(address) = 0x70a08231</summary>
        public static readonly FunctionSelector BalanceOf = new(0x70a08231);

        /// <summary>allowance(address,address) = 0xdd62ed3e</summary>
        public static readonly FunctionSelector Allowance = new(0xdd62ed3e);

        /// <summary>totalSupply() = 0x18160ddd</summary>
        public static readonly FunctionSelector TotalSupply = new(0x18160ddd);
    }

    /// <summary>
    /// Common ERC-721 selectors (compile-time constants).
    /// </summary>
    public static class Erc721
    {
        /// <summary>safeTransferFrom(address,address,uint256) = 0x42842e0e</summary>
        public static readonly FunctionSelector SafeTransferFrom = new(0x42842e0e);

        /// <summary>safeTransferFrom(address,address,uint256,bytes) = 0xb88d4fde</summary>
        public static readonly FunctionSelector SafeTransferFromWithData = new(0xb88d4fde);

        /// <summary>ownerOf(uint256) = 0x6352211e</summary>
        public static readonly FunctionSelector OwnerOf = new(0x6352211e);

        /// <summary>tokenURI(uint256) = 0xc87b56dd</summary>
        public static readonly FunctionSelector TokenUri = new(0xc87b56dd);
    }
}