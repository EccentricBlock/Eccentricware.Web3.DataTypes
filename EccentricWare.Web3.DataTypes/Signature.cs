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
/// The type of cryptographic signature.
/// </summary>
public enum SignatureType : byte
{
    /// <summary>
    /// EVM ECDSA signature (65 bytes: r + s + v).
    /// </summary>
    Evm = 0,
    
    /// <summary>
    /// Solana Ed25519 signature (64 bytes).
    /// </summary>
    Solana = 1
}

/// <summary>
/// A cryptographic signature supporting both EVM (65 bytes) and Solana (64 bytes) formats.
/// Optimised for minimal allocations and predictable hot-path performance.
/// </summary>
/// <remarks>
/// Storage layout:
/// - 64 bytes: signature payload (EVM: r||s, Solana: ed25519 signature)
/// - 1 byte:  EVM recovery identifier / parity byte (meaning depends on ingestion policy)
/// - 1 byte:  <see cref="SignatureType"/>
/// - padding: explicit to ensure 72-byte stride in arrays (8-byte aligned per element)
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(SignatureJsonConverter))]
public readonly struct Signature :
    IEquatable<Signature>,
    IComparable<Signature>,
    IComparable,
    ISpanFormattable,
    ISpanParsable<Signature>,
    IUtf8SpanFormattable
{
    /// <summary>
    /// The size in bytes of an EVM signature (65 bytes).
    /// </summary>
    public const int EvmByteLength = 65;

    /// <summary>
    /// The size in bytes of a Solana signature (64 bytes).
    /// </summary>
    public const int SolanaByteLength = 64;

    /// <summary>
    /// The size in hex characters of an EVM signature without a 0x prefix (130 chars).
    /// </summary>
    public const int EvmHexLength = 130;

    /// <summary>
    /// The size in hex characters of a Solana signature without a 0x prefix (128 chars).
    /// </summary>
    public const int SolanaHexLength = 128;

    /// <summary>
    /// Typical maximum Base58 length for a 64-byte value (Solana signatures are commonly 87–88 chars).
    /// Used only as a DoS hygiene cap during parsing.
    /// </summary>
    public const int SolanaMaxBase58Length = 96;

    private readonly ulong _u0; // bytes 0-7
    private readonly ulong _u1; // bytes 8-15
    private readonly ulong _u2; // bytes 16-23
    private readonly ulong _u3; // bytes 24-31
    private readonly ulong _u4; // bytes 32-39
    private readonly ulong _u5; // bytes 40-47
    private readonly ulong _u6; // bytes 48-55
    private readonly ulong _u7; // bytes 56-63

    private readonly byte _v;           // EVM-only: recovery identifier / parity byte
    private readonly SignatureType _type;

    // Explicit padding to ensure deterministic 72-byte layout and 8-byte array stride.
    private readonly ushort _pad16;
    private readonly uint _pad32;

    /// <summary>
    /// A zero signature value (defaults to <see cref="SignatureType.Evm"/> with all-zero payload).
    /// </summary>
    public static readonly Signature Zero = ZeroEvm;

    /// <summary>
    /// Creates a signature from raw fields.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Signature(
        ulong u0, ulong u1, ulong u2, ulong u3,
        ulong u4, ulong u5, ulong u6, ulong u7,
        byte v,
        SignatureType type)
    {
        _u0 = u0; _u1 = u1; _u2 = u2; _u3 = u3;
        _u4 = u4; _u5 = u5; _u6 = u6; _u7 = u7;
        _v = v;
        _type = type;
        _pad16 = 0;
        _pad32 = 0;
    }

    /// <summary>
    /// Gets the signature type (EVM or Solana).
    /// </summary>
    public SignatureType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type;
    }

    /// <summary>
    /// Gets the encoded byte length of this signature (65 for EVM, 64 for Solana).
    /// </summary>
    public int ByteLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == SignatureType.Evm ? EvmByteLength : SolanaByteLength;
    }

    /// <summary>
    /// Gets the encoded hex length (without 0x prefix): 130 for EVM, 128 for Solana.
    /// </summary>
    public int HexLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == SignatureType.Evm ? EvmHexLength : SolanaHexLength;
    }

    /// <summary>
    /// Returns <c>true</c> if the signature payload and metadata are all zero.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3 | _u4 | _u5 | _u6 | _u7 | _v) == 0;
    }

    /// <summary>
    /// Gets the EVM recovery identifier / parity byte.
    /// Returns 0 for Solana signatures.
    /// </summary>
    public byte V
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == SignatureType.Evm ? _v : (byte)0;
    }

    /// <summary>
    /// Creates an EVM signature from r (32 bytes), s (32 bytes), and v (1 byte).
    /// </summary>
    public static Signature FromEvmComponents(ReadOnlySpan<byte> r32, ReadOnlySpan<byte> s32, byte v)
    {
        if (r32.Length != 32)
            ThrowHelper.ThrowArgumentExceptionInvalidSignatureRLength(nameof(r32));
        if (s32.Length != 32)
            ThrowHelper.ThrowArgumentExceptionInvalidSignatureSLength(nameof(s32));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(r32),
            BinaryPrimitives.ReadUInt64BigEndian(r32.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(r32.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(r32.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(s32),
            BinaryPrimitives.ReadUInt64BigEndian(s32.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(s32.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(s32.Slice(24)),
            v,
            SignatureType.Evm);
    }

    /// <summary>
    /// Creates an EVM signature from a 65-byte span (r || s || v).
    /// </summary>
    public static Signature FromEvmBytes(ReadOnlySpan<byte> signature65)
    {
        if (signature65.Length != EvmByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidEvmSignatureLength(nameof(signature65));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(signature65),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(32)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(40)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(48)),
            BinaryPrimitives.ReadUInt64BigEndian(signature65.Slice(56)),
            signature65[64],
            SignatureType.Evm);
    }

    /// <summary>
    /// Creates a Solana signature from a 64-byte span.
    /// </summary>
    public static Signature FromSolanaBytes(ReadOnlySpan<byte> signature64)
    {
        if (signature64.Length != SolanaByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidSolanaSignatureLength(nameof(signature64));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(signature64),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(32)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(40)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(48)),
            BinaryPrimitives.ReadUInt64BigEndian(signature64.Slice(56)),
            0,
            SignatureType.Solana);
    }

    /// <summary>
    /// Writes the raw signature bytes.
    /// EVM writes 65 bytes (r || s || v), Solana writes 64 bytes.
    /// </summary>
    public void WriteBytes(Span<byte> destinationBytes)
    {
        int requiredLength = ByteLength;
        if (destinationBytes.Length < requiredLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destinationBytes));

        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(24), _u3);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(32), _u4);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(40), _u5);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(48), _u6);
        BinaryPrimitives.WriteUInt64BigEndian(destinationBytes.Slice(56), _u7);

        if (_type == SignatureType.Evm)
            destinationBytes[64] = _v;
    }

    /// <summary>
    /// Returns the signature as a newly allocated byte array.
    /// Intended for cold paths (e.g., persistence, diagnostics).
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets the EVM r component as a <c>Hash32</c> (first 32 bytes of the payload).
    /// </summary>
    public Hash32 R => new(_u0, _u1, _u2, _u3);

    /// <summary>
    /// Gets the EVM s component as a <c>Hash32</c> (second 32 bytes of the payload).
    /// </summary>
    public Hash32 S => new(_u4, _u5, _u6, _u7);

    /// <summary>
    /// Writes the EVM r component (32 bytes) to the destination span.
    /// For Solana signatures, this writes the first 32 bytes of the signature payload.
    /// </summary>
    public void WriteR(Span<byte> destination32)
    {
        if (destination32.Length < 32)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination32));

        BinaryPrimitives.WriteUInt64BigEndian(destination32, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(24), _u3);
    }

    /// <summary>
    /// Writes the EVM s component (32 bytes) to the destination span.
    /// For Solana signatures, this writes bytes 32..63 of the signature payload.
    /// </summary>
    public void WriteS(Span<byte> destination32)
    {
        if (destination32.Length < 32)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination32));

        BinaryPrimitives.WriteUInt64BigEndian(destination32, _u4);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(8), _u5);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(16), _u6);
        BinaryPrimitives.WriteUInt64BigEndian(destination32.Slice(24), _u7);
    }

    /// <summary>
    /// Checks equality by signature payload plus type, and v for EVM.
    /// Implemented as a branch-minimal XOR/OR reduction for hot-path efficiency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Signature other)
    {
        if (_type != other._type) return false;
        if (_type == SignatureType.Evm && _v != other._v) return false;

        ulong diff =
            (_u0 ^ other._u0) | (_u1 ^ other._u1) | (_u2 ^ other._u2) | (_u3 ^ other._u3) |
            (_u4 ^ other._u4) | (_u5 ^ other._u5) | (_u6 ^ other._u6) | (_u7 ^ other._u7);

        return diff == 0;
    }

    /// <summary>
    /// Checks equality against an object.
    /// </summary>
    public override bool Equals(object? obj) => obj is Signature other && Equals(other);

    /// <summary>
    /// Computes a hash code consistent with <see cref="Equals(Signature)"/>.
    /// </summary>
    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(_u0); hc.Add(_u1); hc.Add(_u2); hc.Add(_u3);
        hc.Add(_u4); hc.Add(_u5); hc.Add(_u6); hc.Add(_u7);
        hc.Add((byte)_type);
        hc.Add(_type == SignatureType.Evm ? _v : (byte)0);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Signature left, Signature right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Signature left, Signature right) => !left.Equals(right);

    /// <summary>
    /// Lexicographic comparison by type, then payload, then v for EVM.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Signature other)
    {
        if (_type != other._type)
            return _type < other._type ? -1 : 1;

        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        if (_u4 != other._u4) return _u4 < other._u4 ? -1 : 1;
        if (_u5 != other._u5) return _u5 < other._u5 ? -1 : 1;
        if (_u6 != other._u6) return _u6 < other._u6 ? -1 : 1;
        if (_u7 != other._u7) return _u7 < other._u7 ? -1 : 1;

        if (_type == SignatureType.Evm && _v != other._v)
            return _v < other._v ? -1 : 1;

        return 0;
    }

    /// <summary>
    /// Non-generic comparison.
    /// </summary>
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Signature other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Signature)}", nameof(obj));
    }

    /// <summary>
    /// Less-than operator.
    /// </summary>
    public static bool operator <(Signature left, Signature right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Greater-than operator.
    /// </summary>
    public static bool operator >(Signature left, Signature right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Less-than-or-equal operator.
    /// </summary>
    public static bool operator <=(Signature left, Signature right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Greater-than-or-equal operator.
    /// </summary>
    public static bool operator >=(Signature left, Signature right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Parses a signature from a hex string (with optional 0x prefix) or a Solana Base58 signature.
    /// </summary>
    public static Signature Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out Signature value))
            ThrowHelper.ThrowFormatExceptionInvalidSignatureHexLength();

        return value;
    }

    /// <summary>
    /// Parses a signature from UTF-8 (typically a JSON string token value).
    /// Accepts 0x-prefixed hex, bare hex, or Solana Base58 for 64-byte signatures.
    /// </summary>
    public static Signature Parse(ReadOnlySpan<byte> utf8)
    {
        if (!TryParse(utf8, out Signature value))
            ThrowHelper.ThrowFormatExceptionInvalidSignatureHexLength();

        return value;
    }

    /// <summary>
    /// Tries to parse a signature from chars without allocating.
    /// Accepts 0x-prefixed hex, bare hex, or Solana Base58.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out Signature value)
    {
        value = Zero;

        text = ByteUtils.TrimAsciiWhitespace(text);

        if (text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            text = text.Slice(2);

        // Hex fast-path (fixed widths).
        if (text.Length == EvmHexLength)
        {
            if (!TryParseHexPayload64Chars(text.Slice(0, 128), out ulong u0, out ulong u1, out ulong u2, out ulong u3, out ulong u4, out ulong u5, out ulong u6, out ulong u7))
                return false;

            if (!ByteUtils.TryParseHexByteCharsFixed2(text.Slice(128, 2), out byte v))
                return false;

            value = new Signature(u0, u1, u2, u3, u4, u5, u6, u7, v, SignatureType.Evm);
            return true;
        }

        if (text.Length == SolanaHexLength)
        {
            if (!TryParseHexPayload64Chars(text, out ulong u0, out ulong u1, out ulong u2, out ulong u3, out ulong u4, out ulong u5, out ulong u6, out ulong u7))
                return false;

            value = new Signature(u0, u1, u2, u3, u4, u5, u6, u7, 0, SignatureType.Solana);
            return true;
        }

        // Solana Base58 (cold-ish, but needed for Solana JSON-RPC interoperability).
        if (text.Length > 0 && text.Length <= SolanaMaxBase58Length)
        {
            Span<byte> bytes64 = stackalloc byte[SolanaByteLength];
            if (!ByteUtils.TryDecodeBase58To64Chars(text, bytes64))
                return false;

            value = FromSolanaBytes(bytes64);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to parse a signature from UTF-8 without allocating.
    /// Accepts quoted JSON string values, 0x-prefixed hex, bare hex, or Solana Base58.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8, out Signature value)
    {
        value = Zero;

        utf8 = ByteUtils.TrimAsciiWhitespaceUtf8(utf8);
        if (!ByteUtils.TryUnquoteJsonUtf8(utf8, out ReadOnlySpan<byte> token))
            return false;

        if (token.Length >= 2 && token[0] == (byte)'0' && ((token[1] | 0x20) == (byte)'x'))
            token = token.Slice(2);

        // Hex fast-path (fixed widths).
        if (token.Length == EvmHexLength)
        {
            if (!TryParseHexPayload64Utf8(token.Slice(0, 128), out ulong u0, out ulong u1, out ulong u2, out ulong u3, out ulong u4, out ulong u5, out ulong u6, out ulong u7))
                return false;

            if (!ByteUtils.TryParseHexByteUtf8Fixed2(token.Slice(128, 2), out byte v))
                return false;

            value = new Signature(u0, u1, u2, u3, u4, u5, u6, u7, v, SignatureType.Evm);
            return true;
        }

        if (token.Length == SolanaHexLength)
        {
            if (!TryParseHexPayload64Utf8(token, out ulong u0, out ulong u1, out ulong u2, out ulong u3, out ulong u4, out ulong u5, out ulong u6, out ulong u7))
                return false;

            value = new Signature(u0, u1, u2, u3, u4, u5, u6, u7, 0, SignatureType.Solana);
            return true;
        }

        // Solana Base58.
        if (token.Length > 0 && token.Length <= SolanaMaxBase58Length)
        {
            Span<byte> bytes64 = stackalloc byte[SolanaByteLength];
            if (!ByteUtils.TryDecodeBase58To64(token, bytes64))
                return false;

            value = FromSolanaBytes(bytes64);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats to a 0x-prefixed lowercase hex string.
    /// Intended for cold paths (logging, debugging, serialisation).
    /// </summary>
    public override string ToString()
    {
        int hexLength = HexLength;
        int totalLength = hexLength + 2;

        return string.Create(totalLength, this, static (chars, sig) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            sig.FormatHexChars(chars.Slice(2), uppercase: false);
        });
    }

    /// <summary>
    /// Formats to hex with optional prefix and casing.
    /// Supported formats:
    /// - empty / "0x" / "0X": include 0x prefix
    /// - "x" / "X": no prefix, lowercase/uppercase
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        ReadOnlySpan<char> fmt = format.AsSpanSafe();
        bool hasPrefix = fmt.Length == 0 || (fmt.Length == 2 && fmt[0] == '0' && (fmt[1] == 'x' || fmt[1] == 'X'));
        bool uppercase = (fmt.Length == 1 && fmt[0] == 'X') || (fmt.Length == 2 && fmt[0] == '0' && fmt[1] == 'X');

        int hexLength = HexLength;
        int totalLength = hasPrefix ? hexLength + 2 : hexLength;

        return string.Create(totalLength, (this, hasPrefix, uppercase), static (chars, state) =>
        {
            int offset = 0;
            if (state.hasPrefix)
            {
                chars[0] = '0';
                chars[1] = 'x';
                offset = 2;
            }

            state.Item1.FormatHexChars(chars.Slice(offset), state.uppercase);
        });
    }

    /// <summary>
    /// Tries to format to a character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || (format.Length == 2 && format[0] == '0' && (format[1] == 'x' || format[1] == 'X'));
        bool uppercase = (format.Length == 1 && format[0] == 'X') || (format.Length == 2 && format[0] == '0' && format[1] == 'X');

        int hexLength = HexLength;
        int required = hasPrefix ? hexLength + 2 : hexLength;

        if (destination.Length < required)
        {
            charsWritten = 0;
            return false;
        }

        int offset = 0;
        if (hasPrefix)
        {
            destination[0] = '0';
            destination[1] = 'x';
            offset = 2;
        }

        FormatHexChars(destination.Slice(offset), uppercase);
        charsWritten = required;
        return true;
    }

    /// <summary>
    /// Tries to format to a UTF-8 span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || (format.Length == 2 && format[0] == '0' && (format[1] == 'x' || format[1] == 'X'));
        bool uppercase = (format.Length == 1 && format[0] == 'X') || (format.Length == 2 && format[0] == '0' && format[1] == 'X');

        int hexLength = HexLength;
        int required = hasPrefix ? hexLength + 2 : hexLength;

        if (utf8Destination.Length < required)
        {
            bytesWritten = 0;
            return false;
        }

        int offset = 0;
        if (hasPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)'x';
            offset = 2;
        }

        FormatHexUtf8(utf8Destination.Slice(offset), uppercase);
        bytesWritten = required;
        return true;
    }

    /// <summary>
    /// Parses from a string.
    /// </summary>
    public static Signature Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses from a span (provider is ignored; parsing is invariant).
    /// </summary>
    public static Signature Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse from a string.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Signature result)
        => TryParse(s.AsSpanSafe(), out result);

    /// <summary>
    /// Tries to parse from a span (provider is ignored; parsing is invariant).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Signature result)
        => TryParse(s, out result);

    /// <summary>
    /// Creates a zero EVM signature.
    /// </summary>
    public static Signature ZeroEvm => new(0, 0, 0, 0, 0, 0, 0, 0, 0, SignatureType.Evm);

    /// <summary>
    /// Creates a zero Solana signature.
    /// </summary>
    public static Signature ZeroSolana => new(0, 0, 0, 0, 0, 0, 0, 0, 0, SignatureType.Solana);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexChars(Span<char> destination, bool uppercase)
    {
        if (destination.Length < HexLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        // 8 * 16 chars = 128
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(0, 16), _u0, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(16, 16), _u1, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(32, 16), _u2, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(48, 16), _u3, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(64, 16), _u4, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(80, 16), _u5, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(96, 16), _u6, uppercase);
        ByteUtils.WriteHexUInt64CharsFixed16(destination.Slice(112, 16), _u7, uppercase);

        if (_type == SignatureType.Evm)
            ByteUtils.WriteHexByteCharsFixed2(destination.Slice(128, 2), _v, uppercase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatHexUtf8(Span<byte> destination, bool uppercase)
    {
        if (destination.Length < HexLength)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        ReadOnlySpan<byte> alphabet = uppercase ? ByteUtils.HexBytesUpper : ByteUtils.HexBytesLower;

        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(0, 16), _u0, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(16, 16), _u1, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(32, 16), _u2, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(48, 16), _u3, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(64, 16), _u4, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(80, 16), _u5, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(96, 16), _u6, alphabet);
        ByteUtils.WriteHexUInt64Utf8Fixed16(destination.Slice(112, 16), _u7, alphabet);

        if (_type == SignatureType.Evm)
            ByteUtils.WriteHexByteUtf8Fixed2(destination.Slice(128, 2), _v, alphabet);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexPayload64Utf8(
        ReadOnlySpan<byte> hex128,
        out ulong u0, out ulong u1, out ulong u2, out ulong u3,
        out ulong u4, out ulong u5, out ulong u6, out ulong u7)
    {
        u0 = u1 = u2 = u3 = u4 = u5 = u6 = u7 = 0;

        if (hex128.Length != 128)
            return false;

        return
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(0, 16), out u0) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(16, 16), out u1) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(32, 16), out u2) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(48, 16), out u3) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(64, 16), out u4) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(80, 16), out u5) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(96, 16), out u6) &&
            ByteUtils.TryParseHexUInt64Utf8Fixed16(hex128.Slice(112, 16), out u7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexPayload64Chars(
        ReadOnlySpan<char> hex128,
        out ulong u0, out ulong u1, out ulong u2, out ulong u3,
        out ulong u4, out ulong u5, out ulong u6, out ulong u7)
    {
        u0 = u1 = u2 = u3 = u4 = u5 = u6 = u7 = 0;

        if (hex128.Length != 128)
            return false;

        return
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(0, 16), out u0) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(16, 16), out u1) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(32, 16), out u2) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(48, 16), out u3) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(64, 16), out u4) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(80, 16), out u5) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(96, 16), out u6) &&
            ByteUtils.TryParseHexUInt64CharsFixed16(hex128.Slice(112, 16), out u7);
    }
}