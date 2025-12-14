using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A 4-byte EVM function selector (first 4 bytes of keccak256 hash of function signature).
/// Stored as a single uint for maximum performance in database lookups.
/// 
/// Memory layout: 4 bytes as uint (optimal for CPU registers and cache).
/// Database storage: int32 for fastest possible B-tree indexing.
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
    /// <summary>
    /// The size in bytes of a function selector.
    /// </summary>
    public const int ByteLength = 4;

    /// <summary>
    /// The size in hex characters (without prefix).
    /// </summary>
    public const int HexLength = 8;

    // Stored as big-endian uint for natural hex string ordering
    // This matches how selectors appear in calldata and documentation
    private readonly uint _value;

    /// <summary>
    /// The zero selector.
    /// </summary>
    public static readonly FunctionSelector Zero;

    // Hex lookup tables
    private static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    #region Constructors

    /// <summary>
    /// Creates a FunctionSelector from a uint value (big-endian interpretation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FunctionSelector(uint value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a FunctionSelector from a 4-byte big-endian span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FunctionSelector(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidFunctionSelectorLength(nameof(bytes));
        _value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    /// <summary>
    /// Creates a FunctionSelector from individual bytes (big-endian order).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FunctionSelector(byte b0, byte b1, byte b2, byte b3)
    {
        _value = ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3;
    }

    /// <summary>
    /// Creates a FunctionSelector by hashing a function signature.
    /// e.g., "transfer(address,uint256)" -> 0xa9059cbb
    /// </summary>
    public static FunctionSelector FromSignature(string signature)
    {
        return FromSignature(System.Text.Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>
    /// Creates a FunctionSelector by hashing a function signature (UTF-8 bytes).
    /// </summary>
    public static FunctionSelector FromSignature(ReadOnlySpan<byte> signatureUtf8)
    {
        Span<byte> hash = stackalloc byte[32];
        Keccak256.ComputeHash(signatureUtf8, hash);
        return new FunctionSelector(hash.Slice(0, ByteLength));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the selector as a uint value.
    /// Optimal for database storage as int32/uint32.
    /// </summary>
    public uint Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Gets the selector as a signed int (for database compatibility).
    /// </summary>
    public int AsInt32
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((int)_value);
    }

    /// <summary>
    /// Returns true if this is the zero selector.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value == 0;
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the selector as 4 bytes in big-endian order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));
        BinaryPrimitives.WriteUInt32BigEndian(destination, _value);
    }

    /// <summary>
    /// Returns the selector as a 4-byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Creates from a signed int (for database compatibility).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FunctionSelector FromInt32(int value) => new(unchecked((uint)value));

    #endregion

    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FunctionSelector other) => _value == other._value;

    public override bool Equals(object? obj) => obj is FunctionSelector other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => (int)_value; // Direct use - uint already well-distributed

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FunctionSelector left, FunctionSelector right) => left._value == right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FunctionSelector left, FunctionSelector right) => left._value != right._value;

    #endregion

    #region Comparison

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FunctionSelector other) => _value.CompareTo(other._value);

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is FunctionSelector other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(FunctionSelector)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FunctionSelector left, FunctionSelector right) => left._value < right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FunctionSelector left, FunctionSelector right) => left._value > right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FunctionSelector left, FunctionSelector right) => left._value <= right._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FunctionSelector left, FunctionSelector right) => left._value >= right._value;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses an 8-character hex string (with or without 0x prefix).
    /// </summary>
    public static FunctionSelector Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != HexLength)
            ThrowHelper.ThrowFormatExceptionInvalidFunctionSelectorHexLength();

        uint value = ParseHexUInt32(hex);
        return new FunctionSelector(value);
    }

    public static FunctionSelector Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    public static FunctionSelector Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    public static FunctionSelector Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a hex string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out FunctionSelector result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != HexLength)
            return false;

        if (!TryParseHexUInt32(hex, out uint value))
            return false;

        result = new FunctionSelector(value);
        return true;
    }

    public static bool TryParse(string? hex, out FunctionSelector result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FunctionSelector result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FunctionSelector result)
        => TryParse(s, out result);

    #region Hex Parsing Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexNibble(char c)
    {
        int val = c;
        int digit = val - '0';
        int lower = (val | 0x20) - 'a' + 10;
        
        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ParseHexUInt32(ReadOnlySpan<char> hex)
    {
        uint result = 0;
        for (int i = 0; i < 8; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            result = (result << 4) | (uint)nibble;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexUInt32(ReadOnlySpan<char> hex, out uint result)
    {
        result = 0;
        for (int i = 0; i < 8; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) return false;
            result = (result << 4) | (uint)nibble;
        }
        return true;
    }

    #endregion

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the hex representation with 0x prefix (lowercase).
    /// </summary>
    public override string ToString()
    {
        return string.Create(HexLength + 2, this, static (chars, selector) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            selector.FormatHexCore(chars.Slice(2), uppercase: false);
        });
    }

    /// <summary>
    /// Formats the value according to the format string.
    /// "x" lowercase, "X" uppercase, "0x" with prefix (default), "0X" uppercase with prefix.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        return format switch
        {
            "0x" => ToString(),
            "0X" => string.Create(HexLength + 2, this, static (chars, selector) =>
            {
                chars[0] = '0';
                chars[1] = 'x';
                selector.FormatHexCore(chars.Slice(2), uppercase: true);
            }),
            "x" => string.Create(HexLength, this, static (chars, selector) =>
            {
                selector.FormatHexCore(chars, uppercase: false);
            }),
            "X" => string.Create(HexLength, this, static (chars, selector) =>
            {
                selector.FormatHexCore(chars, uppercase: true);
            }),
            _ => throw new FormatException($"Unknown format: {format}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexCore(Span<char> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        uint v = _value;
        for (int i = 7; i >= 0; i--)
        {
            destination[i] = (char)hexTable[(int)(v & 0xF)];
            v >>= 4;
        }
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? HexLength + 2 : HexLength;

        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            destination[0] = '0';
            destination[1] = 'x';
            FormatHexCore(destination.Slice(2), uppercase);
        }
        else
        {
            FormatHexCore(destination, uppercase);
        }

        charsWritten = requiredLength;
        return true;
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? HexLength + 2 : HexLength;

        if (utf8Destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)'x';
            FormatHexCoreUtf8(utf8Destination.Slice(2), uppercase);
        }
        else
        {
            FormatHexCoreUtf8(utf8Destination, uppercase);
        }

        bytesWritten = requiredLength;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexCoreUtf8(Span<byte> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        uint v = _value;
        for (int i = 7; i >= 0; i--)
        {
            destination[i] = hexTable[(int)(v & 0xF)];
            v >>= 4;
        }
    }

    #endregion

    #region Conversions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FunctionSelector(uint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(FunctionSelector selector) => selector._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(FunctionSelector selector) => selector.AsInt32;

    /// <summary>
    /// Extracts the function selector from calldata (first 4 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FunctionSelector FromCalldata(ReadOnlySpan<byte> calldata)
    {
        if (calldata.Length < ByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidFunctionSelectorLength(nameof(calldata));
        return new FunctionSelector(calldata.Slice(0, ByteLength));
    }

    #endregion

    #region Common Selectors

    /// <summary>
    /// Common ERC-20 function selectors.
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
    /// Common ERC-721 function selectors.
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

    #endregion
}

