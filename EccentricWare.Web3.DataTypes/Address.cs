using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// The type of blockchain address.
/// </summary>
public enum AddressType : byte
{
    /// <summary>
    /// Ethereum Virtual Machine address (20 bytes, hex encoded with 0x prefix).
    /// </summary>
    Evm = 0,
    
    /// <summary>
    /// Solana address (32 bytes, Base58 encoded).
    /// </summary>
    Solana = 1
}

/// <summary>
/// A compact blockchain address supporting both EVM (20 bytes) and Solana (32 bytes).
/// </summary>
/// <remarks>
/// <para>
/// Storage layout is optimised for hot-path comparisons and dictionary keys:
/// 32 bytes of data (4 x ulong) + 1 byte <see cref="AddressType"/> + 7 bytes padding = 40 bytes.
/// </para>
/// <para>
/// For EVM addresses, only the first 20 bytes are used; the remaining 12 bytes are zero.
/// The 32-byte backing value is stored in big-endian byte order across the four ulongs
/// for stable lexicographic ordering and predictable formatting.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
[JsonConverter(typeof(AddressJsonConverter))]
public readonly struct Address :
    IEquatable<Address>,
    IComparable<Address>,
    IComparable,
    ISpanFormattable,
    ISpanParsable<Address>,
    IUtf8SpanFormattable
{
    /// <summary>The byte length of an EVM address payload.</summary>
    public const int EvmByteLength = 20;

    /// <summary>The byte length of a Solana public key payload.</summary>
    public const int SolanaByteLength = 32;

    /// <summary>The number of hex characters in an EVM address without prefix (20 bytes * 2).</summary>
    public const int EvmHexLength = 40;

    /// <summary>The maximum number of Base58 characters for a 32-byte payload.</summary>
    public const int MaxBase58Length = 44;

    // 32 bytes backing payload (big-endian):
    // _u0 contains bytes 0..7 (most significant), _u3 contains bytes 24..31 (least significant).
    private readonly ulong _u0;
    private readonly ulong _u1;
    private readonly ulong _u2;
    private readonly ulong _u3;

    private readonly AddressType _type;

    // Explicit padding to lock the struct size at 40 bytes and keep layout stable for bulk matchers.
    private readonly byte _pad0;
    private readonly byte _pad1;
    private readonly byte _pad2;
    private readonly byte _pad3;
    private readonly byte _pad4;
    private readonly byte _pad5;
    private readonly byte _pad6;

    /// <summary>A zero EVM address (<c>0x000…000</c>).</summary>
    public static readonly Address ZeroEvm = new(0, 0, 0, 0, AddressType.Evm);

    /// <summary>A zero Solana address (32 zero bytes; Base58 would be 32 '1' characters).</summary>
    public static readonly Address ZeroSolana = new(0, 0, 0, 0, AddressType.Solana);

    /// <summary>
    /// Creates an address from 32 bytes represented by four 64-bit words in big-endian order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Address(ulong u0Msb, ulong u1, ulong u2, ulong u3Lsb, AddressType addressType)
    {
        _u0 = u0Msb;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3Lsb;
        _type = addressType;

        _pad0 = _pad1 = _pad2 = _pad3 = _pad4 = _pad5 = _pad6 = 0;
    }

    /// <summary>Returns the address family.</summary>
    public AddressType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type;
    }

    /// <summary>Returns the wire byte length for the current address family (20 for EVM; 32 for Solana).</summary>
    public int ByteLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == AddressType.Evm ? EvmByteLength : SolanaByteLength;
    }

    /// <summary>Returns true if the 32-byte backing value is all zeros (type is not considered).</summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    /// <summary>
    /// Creates an EVM address from a 20-byte big-endian payload.
    /// </summary>
    public static Address FromEvmBytes(ReadOnlySpan<byte> evmAddress20)
    {
        if (evmAddress20.Length != EvmByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidEvmLength(nameof(evmAddress20));

        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(evmAddress20);
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(evmAddress20.Slice(8));
        ulong u2 = (ulong)BinaryPrimitives.ReadUInt32BigEndian(evmAddress20.Slice(16)) << 32;

        return new Address(u0, u1, u2, 0, AddressType.Evm);
    }

    /// <summary>
    /// Creates a Solana address from a 32-byte payload.
    /// </summary>
    public static Address FromSolanaBytes(ReadOnlySpan<byte> solanaPubkey32)
    {
        if (solanaPubkey32.Length != SolanaByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidSolanaLength(nameof(solanaPubkey32));

        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(solanaPubkey32);
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(solanaPubkey32.Slice(8));
        ulong u2 = BinaryPrimitives.ReadUInt64BigEndian(solanaPubkey32.Slice(16));
        ulong u3 = BinaryPrimitives.ReadUInt64BigEndian(solanaPubkey32.Slice(24));

        return new Address(u0, u1, u2, u3, AddressType.Solana);
    }

    /// <summary>
    /// Writes the address bytes in big-endian order to <paramref name="destination"/>.
    /// Writes 20 bytes for EVM and 32 bytes for Solana.
    /// </summary>
    public void WriteBytes(Span<byte> destination)
    {
        int required = ByteLength;
        if (destination.Length < required)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);

        if (_type == AddressType.Evm)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(16), (uint)(_u2 >> 32));
            return;
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Creates a new byte array containing the wire-format bytes for this address (20 or 32 bytes).
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Compares this address to another for equality.
    /// Scalar comparison is faster than SIMD for single-value compares.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Address other)
        => _type == other._type && _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Address other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3, (byte)_type);

    /// <summary>Returns true if two addresses are equal.</summary>
    public static bool operator ==(Address left, Address right) => left.Equals(right);

    /// <summary>Returns true if two addresses are not equal.</summary>
    public static bool operator !=(Address left, Address right) => !left.Equals(right);

    /// <summary>
    /// Lexicographic comparison. EVM addresses sort before Solana addresses.
    /// Within the same family, compares backing bytes from most-significant to least-significant.
    /// </summary>
    public int CompareTo(Address other)
    {
        if (_type != other._type)
            return _type < other._type ? -1 : 1;

        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        return 0;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Address other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Address)}.", nameof(obj));
    }

    /// <summary>Returns true if <paramref name="left"/> is less than <paramref name="right"/>.</summary>
    public static bool operator <(Address left, Address right) => left.CompareTo(right) < 0;

    /// <summary>Returns true if <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    public static bool operator >(Address left, Address right) => left.CompareTo(right) > 0;

    /// <summary>Returns true if <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(Address left, Address right) => left.CompareTo(right) <= 0;

    /// <summary>Returns true if <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(Address left, Address right) => left.CompareTo(right) >= 0;

    // -------------------------
    // Parsing (chars and UTF-8)
    // -------------------------

    /// <summary>
    /// Parses an address from characters, auto-detecting EVM vs Solana.
    /// Detection order:
    /// <list type="number">
    /// <item><description><c>0x</c>/<c>0X</c> prefix => EVM</description></item>
    /// <item><description>Otherwise, try canonical Base58=>32 => Solana</description></item>
    /// <item><description>Otherwise, if length is 40 and all hex => EVM</description></item>
    /// </list>
    /// </summary>
    public static Address Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out Address value))
            ThrowHelper.ThrowFormatExceptionInvalidAddress();
        return value;
    }

    /// <summary>
    /// Parses an address from a string using invariant parsing rules.
    /// </summary>
    public static Address Parse(string text) => Parse(text.AsSpan());

    /// <summary>
    /// Tries to parse an address from characters without throwing.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out Address value)
    {
        Unsafe.SkipInit(out value);

        if (text.IsEmpty)
            goto Fail;

        // EVM: 0x + 40 hex
        if (text.Length == 42 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
        {
            if (TryParseEvmHexChars(text.Slice(2), out value))
                return true;
            goto Fail;
        }

        // Solana: attempt canonical Base58->32 if in plausible length range (includes the ambiguous 40-char case).
        if ((uint)(text.Length - 32) <= (MaxBase58Length - 32))
        {
            if (TryParseSolanaBase58Chars(text, out value))
                return true;
        }

        // EVM: 40 hex without prefix (fallback only, to avoid misclassifying Base58-like inputs).
        if (text.Length == EvmHexLength && ByteUtils.IsAllHexChars(text))
        {
            if (TryParseEvmHexChars(text, out value))
                return true;
        }

    Fail:
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to parse an address from a nullable string.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out Address value)
    {
        if (text is null)
        {
            value = default;
            return false;
        }

        return TryParse(text.AsSpan(), out value);
    }

    /// <summary>
    /// Parses an address from UTF-8 bytes (optionally surrounded by JSON quotes).
    /// </summary>
    public static Address Parse(ReadOnlySpan<byte> utf8)
    {
        if (!TryParse(utf8, out Address value))
            ThrowHelper.ThrowFormatExceptionInvalidAddress();
        return value;
    }

    /// <summary>
    /// Tries to parse an address from UTF-8 bytes (optionally surrounded by JSON quotes) without allocations.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out Address value)
    {
        Unsafe.SkipInit(out value);

        if (!ByteUtils.TryUnquoteJsonUtf8(utf8, out ReadOnlySpan<byte> unquoted))
            goto Fail;

        // EVM: 0x + 40 hex
        if (unquoted.Length == 42 &&
            unquoted[0] == (byte)'0' &&
            ((unquoted[1] | 0x20) == (byte)'x'))
        {
            if (TryParseEvmHexUtf8(unquoted.Slice(2), out value))
                return true;
            goto Fail;
        }

        // Solana: attempt canonical Base58->32 in plausible length range.
        if ((uint)(unquoted.Length - 32) <= (MaxBase58Length - 32))
        {
            if (TryParseSolanaBase58Utf8(unquoted, out value))
                return true;
        }

        // EVM: 40 hex without prefix.
        if (unquoted.Length == EvmHexLength && ByteUtils.IsAllHexUtf8(unquoted))
        {
            if (TryParseEvmHexUtf8(unquoted, out value))
                return true;
        }

    Fail:
        value = default;
        return false;
    }

    /// <summary>ISpanParsable required method; the format provider is ignored.</summary>
    public static Address Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);

    /// <summary>ISpanParsable required method; the format provider is ignored.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Address result) => TryParse(s, out result);

    /// <summary>ISpanParsable required method; the format provider is ignored.</summary>
    public static Address Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>ISpanParsable required method; the format provider is ignored.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Address result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseEvmHexChars(ReadOnlySpan<char> hex40, out Address value)
    {
        Unsafe.SkipInit(out value);

        if (hex40.Length != EvmHexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex40.Slice(0, 16), out ulong u0)) return false;
        if (!ByteUtils.TryParseHexUInt64CharsFixed16(hex40.Slice(16, 16), out ulong u1)) return false;
        if (!ByteUtils.TryParseHexUInt32CharsFixed8(hex40.Slice(32, 8), out uint u2Upper)) return false;

        value = new Address(u0, u1, (ulong)u2Upper << 32, 0, AddressType.Evm);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseEvmHexUtf8(ReadOnlySpan<byte> hex40Utf8, out Address value)
    {
        Unsafe.SkipInit(out value);

        if (hex40Utf8.Length != EvmHexLength)
            return false;

        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(hex40Utf8.Slice(0, 16), out ulong u0)) return false;
        if (!ByteUtils.TryParseHexUInt64Utf8Fixed16(hex40Utf8.Slice(16, 16), out ulong u1)) return false;
        if (!ByteUtils.TryParseHexUInt32Utf8Fixed8(hex40Utf8.Slice(32, 8), out uint u2Upper)) return false;

        value = new Address(u0, u1, (ulong)u2Upper << 32, 0, AddressType.Evm);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseSolanaBase58Utf8(ReadOnlySpan<byte> base58Utf8, out Address value)
    {
        Unsafe.SkipInit(out value);

        if (!ByteUtils.TryDecodeBase58ToUInt256BigEndian(base58Utf8, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            return false;

        value = new Address(u0, u1, u2, u3, AddressType.Solana);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseSolanaBase58Chars(ReadOnlySpan<char> base58Chars, out Address value)
    {
        Unsafe.SkipInit(out value);

        // Convert to ASCII bytes on stack; reject non-ASCII early.
        Span<byte> tmp = stackalloc byte[MaxBase58Length];
        for (int i = 0; i < base58Chars.Length; i++)
        {
            char c = base58Chars[i];
            if (c > 0x7F) // Base58 alphabet is ASCII
                return false;
            tmp[i] = (byte)c;
        }

        return TryParseSolanaBase58Utf8(tmp.Slice(0, base58Chars.Length), out value);
    }

    // -------------------------
    // Formatting
    // -------------------------

    /// <summary>
    /// Returns the canonical string representation:
    /// EVM as lowercase <c>0x</c>-prefixed hex; Solana as Base58.
    /// </summary>
    public override string ToString()
    {
        Span<char> buffer = stackalloc char[MaxBase58Length];

        if (!TryFormat(buffer, out int written, format: default, provider: null))
            return _type == AddressType.Evm ? "0x" + new string('0', EvmHexLength) : string.Empty;

        return new string(buffer.Slice(0, written));
    }

    /// <summary>
    /// Formats the address.
    /// <list type="bullet">
    /// <item><description>EVM supports: <c>"0x"</c>, <c>"0X"</c>, <c>"x"</c>, <c>"X"</c>, <c>"c"</c>/<c>"C"</c> (EIP-55 checksum).</description></item>
    /// <item><description>Solana ignores the format and returns Base58.</description></item>
    /// </list>
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[MaxBase58Length];

        ReadOnlySpan<char> f = format is null ? default : format.AsSpan();
        if (!TryFormat(buffer, out int written, f, formatProvider))
            throw new FormatException(nameof(format));

        return new string(buffer.Slice(0, written));
    }

    /// <summary>
    /// Tries to format the address into <paramref name="destination"/>.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (_type == AddressType.Solana)
        {
            // Base58 UTF-8 -> chars (ASCII copy)
            Span<byte> tmp = stackalloc byte[MaxBase58Length];
            int len = ByteUtils.EncodeBase58UInt256BigEndianToUtf8(_u0, _u1, _u2, _u3, tmp);
            if ((uint)len > (uint)destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            for (int i = 0; i < len; i++)
                destination[i] = (char)tmp[i];

            charsWritten = len;
            return true;
        }

        if (!TryGetEvmFormat(format, out EvmFormat evmFormat))
        {
            charsWritten = 0;
            return false;
        }

        if (evmFormat.Kind == EvmFormatKind.Checksum)
        {
            return TryFormatEvmChecksumChars(destination, out charsWritten);
        }

        int required = evmFormat.WithPrefix ? (EvmHexLength + 2) : EvmHexLength;
        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        int offset = 0;
        if (evmFormat.WithPrefix)
        {
            destination[0] = '0';
            destination[1] = evmFormat.PrefixUppercase ? 'X' : 'x';
            offset = 2;
        }

        WriteEvmHexChars(destination.Slice(offset), evmFormat.HexUppercase);
        charsWritten = required;
        return true;
    }

    /// <summary>
    /// Tries to format the address into UTF-8 bytes without allocations.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (_type == AddressType.Solana)
        {
            int len = ByteUtils.EncodeBase58UInt256BigEndianToUtf8(_u0, _u1, _u2, _u3, utf8Destination);
            if (len < 0)
            {
                bytesWritten = 0;
                return false;
            }

            bytesWritten = len;
            return true;
        }

        if (!TryGetEvmFormat(format, out EvmFormat evmFormat))
        {
            bytesWritten = 0;
            return false;
        }

        if (evmFormat.Kind == EvmFormatKind.Checksum)
        {
            return TryFormatEvmChecksumUtf8(utf8Destination, out bytesWritten);
        }

        int required = evmFormat.WithPrefix ? (EvmHexLength + 2) : EvmHexLength;
        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        int offset = 0;
        if (evmFormat.WithPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = evmFormat.PrefixUppercase ? (byte)'X' : (byte)'x';
            offset = 2;
        }

        WriteEvmHexUtf8(utf8Destination.Slice(offset), evmFormat.HexUppercase);
        bytesWritten = required;
        return true;
    }

    /// <summary>
    /// Returns the EVM address using EIP-55 checksum encoding (mixed-case) with a lowercase <c>0x</c> prefix.
    /// </summary>
    public string ToChecksumString()
    {
        if (_type != AddressType.Evm)
            throw new InvalidOperationException("Checksum encoding only applies to EVM addresses.");

        Span<char> buffer = stackalloc char[EvmHexLength + 2];
        if (!TryFormatEvmChecksumChars(buffer, out int written) || written != EvmHexLength + 2)
            ThrowHelper.ThrowFormatExceptionInvalidAddress();

        return new string(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteEvmHexChars(Span<char> hex40, bool uppercase)
    {
        // _u0 (16 hex) + _u1 (16 hex) + upper 32 bits of _u2 (8 hex)
        ByteUtils.WriteHexUInt64CharsFixed16(hex40.Slice(0, 16), _u0, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(hex40.Slice(16, 16), _u1, uppercase);
        ByteUtils.WriteHexUInt32CharsFixed8(hex40.Slice(32, 8), (uint)(_u2 >> 32), uppercase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteEvmHexUtf8(Span<byte> hex40Utf8, bool uppercase)
    {
        ReadOnlySpan<byte> alphabet = uppercase ? ByteUtils.HexBytesUpper : ByteUtils.HexBytesLower;

        ByteUtils.WriteHexUInt64Utf8Fixed16(hex40Utf8.Slice(0, 16), _u0, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(hex40Utf8.Slice(16, 16), _u1, alphabet);
        ByteUtils.WriteHexUInt32Utf8Fixed8(hex40Utf8.Slice(32, 8), (uint)(_u2 >> 32), uppercase);
    }

    private bool TryFormatEvmChecksumChars(Span<char> destination, out int charsWritten)
    {
        // Always "0x" + 40 = 42.
        const int required = EvmHexLength + 2;
        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = '0';
        destination[1] = 'x';

        Span<char> hexLower = destination.Slice(2, EvmHexLength);
        WriteEvmHexChars(hexLower, uppercase: false);

        // Hash lowercase hex ASCII.
        Span<byte> asciiHex = stackalloc byte[EvmHexLength];
        for (int i = 0; i < EvmHexLength; i++)
            asciiHex[i] = (byte)hexLower[i];

        Span<byte> hash32 = stackalloc byte[32];
        Keccak256.ComputeHash(asciiHex, hash32);

        // Apply EIP-55 case rules.
        // Convert keccak hash bytes into lowercase hex ascii to avoid byte/nibble ordering ambiguity,
        // then use hex char >= '8' to decide uppercase for corresponding address hex digit.
        Span<char> hashHex = stackalloc char[64]; // 32 bytes -> 64 hex chars
        for (int j = 0; j < 32; j++)
        {
            byte hb = hash32[j];
            int hi = hb >> 4;
            int lo = hb & 0x0F;
            hashHex[j * 2] = (char)(hi < 10 ? ('0' + hi) : ('a' + (hi - 10)));
            hashHex[j * 2 + 1] = (char)(lo < 10 ? ('0' + lo) : ('a' + (lo - 10)));
        }

        for (int i = 0; i < EvmHexLength; i++)
        {
            char c = hexLower[i];
            if ((uint)(c - 'a') <= 5u)
            {
                char hh = hashHex[i];
                if (hh >= '8')
                    hexLower[i] = (char)(c - 32);
            }
        }

        charsWritten = required;
        return true;
    }

    private bool TryFormatEvmChecksumUtf8(Span<byte> utf8Destination, out int bytesWritten)
    {
        const int required = EvmHexLength + 2;
        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        // Compute checksum in chars (ASCII) then copy to UTF-8.
        Span<char> tmp = stackalloc char[required];
        if (!TryFormatEvmChecksumChars(tmp, out int written) || written != required)
        {
            bytesWritten = 0;
            return false;
        }

        for (int i = 0; i < required; i++)
            utf8Destination[i] = (byte)tmp[i];

        bytesWritten = required;
        return true;
    }

    private static bool TryGetEvmFormat(ReadOnlySpan<char> format, out EvmFormat evmFormat)
    {
        // Default: "0x" lowercase prefix + lowercase digits.
        if (format.IsEmpty)
        {
            evmFormat = EvmFormat.Default;
            return true;
        }

        // Single-char formats.
        if (format.Length == 1)
        {
            char f = format[0];
            if (f == 'x') { evmFormat = new EvmFormat(withPrefix: false, prefixUppercase: false, hexUppercase: false, kind: EvmFormatKind.Hex); return true; }
            if (f == 'X') { evmFormat = new EvmFormat(withPrefix: false, prefixUppercase: false, hexUppercase: true, kind: EvmFormatKind.Hex); return true; }
            if (f == 'c' || f == 'C') { evmFormat = new EvmFormat(withPrefix: true, prefixUppercase: false, hexUppercase: false, kind: EvmFormatKind.Checksum); return true; }
        }

        // Two-char prefix formats.
        if (format.Length == 2 && format[0] == '0' && (format[1] == 'x' || format[1] == 'X'))
        {
            bool prefixUpper = format[1] == 'X';
            // Convention: "0X" implies uppercase digits.
            evmFormat = new EvmFormat(withPrefix: true, prefixUppercase: prefixUpper, hexUppercase: prefixUpper, kind: EvmFormatKind.Hex);
            return true;
        }

        evmFormat = default;
        return false;
    }

    private enum EvmFormatKind : byte
    {
        Hex = 0,
        Checksum = 1
    }

    private readonly struct EvmFormat(bool withPrefix, bool prefixUppercase, bool hexUppercase, Address.EvmFormatKind kind)
    {
        public static readonly EvmFormat Default = new(withPrefix: true, prefixUppercase: false, hexUppercase: false, kind: EvmFormatKind.Hex);

        public readonly bool WithPrefix = withPrefix;
        public readonly bool PrefixUppercase = prefixUppercase;
        public readonly bool HexUppercase = hexUppercase;
        public readonly EvmFormatKind Kind = kind;
    }
}