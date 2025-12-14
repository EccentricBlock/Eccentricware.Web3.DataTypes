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
/// Compact representation optimized for minimal memory footprint.
/// 
/// Memory layout: 72 bytes (8 x ulong + 1 byte v + 1 byte type, padded).
/// EVM: 32 bytes (r) + 32 bytes (s) + 1 byte (v)
/// Solana: 64 bytes (Ed25519 signature)
/// </summary>
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
    /// The size in hex characters of an EVM signature (without prefix).
    /// </summary>
    public const int EvmHexLength = 130;

    /// <summary>
    /// The size in hex characters of a Solana signature (without prefix).
    /// </summary>
    public const int SolanaHexLength = 128;

    // Store as 8 x ulong for 64 bytes (r + s for EVM, full sig for Solana)
    // Plus v byte for EVM and type byte
    private readonly ulong _u0; // bytes 0-7
    private readonly ulong _u1; // bytes 8-15
    private readonly ulong _u2; // bytes 16-23
    private readonly ulong _u3; // bytes 24-31 (end of r for EVM)
    private readonly ulong _u4; // bytes 32-39
    private readonly ulong _u5; // bytes 40-47
    private readonly ulong _u6; // bytes 48-55
    private readonly ulong _u7; // bytes 56-63 (end of s for EVM)
    private readonly byte _v;   // Recovery byte for EVM (0, 1, 27, or 28)
    private readonly SignatureType _type;

    /// <summary>
    /// The zero signature.
    /// </summary>
    public static readonly Signature Zero;

    // Hex lookup tables
    private static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    #region Constructors

    /// <summary>
    /// Creates a signature from raw components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Signature(ulong u0, ulong u1, ulong u2, ulong u3, 
                      ulong u4, ulong u5, ulong u6, ulong u7, 
                      byte v, SignatureType type)
    {
        _u0 = u0; _u1 = u1; _u2 = u2; _u3 = u3;
        _u4 = u4; _u5 = u5; _u6 = u6; _u7 = u7;
        _v = v;
        _type = type;
    }

    /// <summary>
    /// Creates an EVM signature from r, s, and v components.
    /// </summary>
    public static Signature FromEvmComponents(ReadOnlySpan<byte> r, ReadOnlySpan<byte> s, byte v)
    {
        if (r.Length != 32)
            ThrowHelper.ThrowArgumentExceptionInvalidSignatureRLength(nameof(r));
        if (s.Length != 32)
            ThrowHelper.ThrowArgumentExceptionInvalidSignatureSLength(nameof(s));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(r),
            BinaryPrimitives.ReadUInt64BigEndian(r.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(r.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(r.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(s),
            BinaryPrimitives.ReadUInt64BigEndian(s.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(s.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(s.Slice(24)),
            v,
            SignatureType.Evm);
    }

    /// <summary>
    /// Creates an EVM signature from a 65-byte span (r + s + v).
    /// </summary>
    public static Signature FromEvmBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != EvmByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidEvmSignatureLength(nameof(bytes));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(bytes),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(32)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(40)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(48)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(56)),
            bytes[64],
            SignatureType.Evm);
    }

    /// <summary>
    /// Creates a Solana signature from a 64-byte span.
    /// </summary>
    public static Signature FromSolanaBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SolanaByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidSolanaSignatureLength(nameof(bytes));

        return new Signature(
            BinaryPrimitives.ReadUInt64BigEndian(bytes),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(32)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(40)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(48)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(56)),
            0,
            SignatureType.Solana);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the type of this signature (EVM or Solana).
    /// </summary>
    public SignatureType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type;
    }

    /// <summary>
    /// Gets the byte length of this signature (65 for EVM, 64 for Solana).
    /// </summary>
    public int ByteLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == SignatureType.Evm ? EvmByteLength : SolanaByteLength;
    }

    /// <summary>
    /// Gets the hex length of this signature (130 for EVM, 128 for Solana).
    /// </summary>
    public int HexLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == SignatureType.Evm ? EvmHexLength : SolanaHexLength;
    }

    /// <summary>
    /// Returns true if this is the zero signature.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3 | _u4 | _u5 | _u6 | _u7 | _v) == 0;
    }

    /// <summary>
    /// Gets the recovery byte (v) for EVM signatures.
    /// Returns 0 for Solana signatures.
    /// </summary>
    public byte V
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _v;
    }

    /// <summary>
    /// Gets the normalized V value (0 or 1) for EVM signatures.
    /// Handles legacy values (27, 28) and EIP-155 values.
    /// </summary>
    public byte VNormalized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_type != SignatureType.Evm) return 0;
            // Handle legacy (27/28), modern (0/1), and EIP-155 values
            if (_v <= 1) return _v;
            if (_v == 27 || _v == 28) return (byte)(_v - 27);
            // EIP-155: v = chainId * 2 + 35 + recovery, so recovery = (v - 35) % 2
            return (byte)((_v - 35) % 2);
        }
    }

    #endregion

    #region EVM Component Access

    /// <summary>
    /// Gets the r component (32 bytes) for EVM signatures.
    /// </summary>
    public void WriteR(Span<byte> destination)
    {
        if (destination.Length < 32)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Gets the s component (32 bytes) for EVM signatures.
    /// </summary>
    public void WriteS(Span<byte> destination)
    {
        if (destination.Length < 32)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u4);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u5);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u6);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u7);
    }

    /// <summary>
    /// Gets the r component as a Hash32.
    /// </summary>
    public Hash32 R => new(_u0, _u1, _u2, _u3);

    /// <summary>
    /// Gets the s component as a Hash32.
    /// </summary>
    public Hash32 S => new(_u4, _u5, _u6, _u7);

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the signature bytes.
    /// EVM: 65 bytes (r + s + v). Solana: 64 bytes.
    /// </summary>
    public void WriteBytes(Span<byte> destination)
    {
        int length = ByteLength;
        if (destination.Length < length)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(32), _u4);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(40), _u5);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(48), _u6);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(56), _u7);

        if (_type == SignatureType.Evm)
            destination[64] = _v;
    }

    /// <summary>
    /// Returns the signature as a byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    #endregion

    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Signature other)
    {
        if (_type != other._type) return false;
        if (_type == SignatureType.Evm && _v != other._v) return false;

        // SIMD comparison for the 64 bytes
        if (Vector256.IsHardwareAccelerated)
        {
            var left1 = Vector256.Create(_u0, _u1, _u2, _u3);
            var right1 = Vector256.Create(other._u0, other._u1, other._u2, other._u3);
            var left2 = Vector256.Create(_u4, _u5, _u6, _u7);
            var right2 = Vector256.Create(other._u4, other._u5, other._u6, other._u7);
            return left1.Equals(right1) && left2.Equals(right2);
        }

        return _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3 &&
               _u4 == other._u4 && _u5 == other._u5 && _u6 == other._u6 && _u7 == other._u7;
    }

    public override bool Equals(object? obj) => obj is Signature other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3, _u4, _u5, _u6, _u7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Signature left, Signature right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Signature left, Signature right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison. EVM signatures sort before Solana signatures.
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

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Signature other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Signature)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Signature left, Signature right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Signature left, Signature right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Signature left, Signature right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Signature left, Signature right) => left.CompareTo(right) >= 0;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a signature hex string. Automatically detects EVM (130 chars) or Solana (128 chars).
    /// </summary>
    public static Signature Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == EvmHexLength)
            return ParseEvmHex(hex);
        if (hex.Length == SolanaHexLength)
            return ParseSolanaHex(hex);

        ThrowHelper.ThrowFormatExceptionInvalidSignatureHexLength();
        return default; // Unreachable
    }

    private static Signature ParseEvmHex(ReadOnlySpan<char> hex)
    {
        Span<byte> bytes = stackalloc byte[EvmByteLength];
        for (int i = 0; i < EvmByteLength; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            bytes[i] = (byte)((hi << 4) | lo);
        }
        return FromEvmBytes(bytes);
    }

    private static Signature ParseSolanaHex(ReadOnlySpan<char> hex)
    {
        Span<byte> bytes = stackalloc byte[SolanaByteLength];
        for (int i = 0; i < SolanaByteLength; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            bytes[i] = (byte)((hi << 4) | lo);
        }
        return FromSolanaBytes(bytes);
    }

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

    public static Signature Parse(string hex) => Parse(hex.AsSpan(), CultureInfo.InvariantCulture);

    public static Signature Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    public static Signature Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse a signature hex string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out Signature result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length == EvmHexLength)
            return TryParseEvmHex(hex, out result);
        if (hex.Length == SolanaHexLength)
            return TryParseSolanaHex(hex, out result);

        return false;
    }

    private static bool TryParseEvmHex(ReadOnlySpan<char> hex, out Signature result)
    {
        result = Zero;
        Span<byte> bytes = stackalloc byte[EvmByteLength];
        for (int i = 0; i < EvmByteLength; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) return false;
            bytes[i] = (byte)((hi << 4) | lo);
        }
        result = FromEvmBytes(bytes);
        return true;
    }

    private static bool TryParseSolanaHex(ReadOnlySpan<char> hex, out Signature result)
    {
        result = Zero;
        Span<byte> bytes = stackalloc byte[SolanaByteLength];
        for (int i = 0; i < SolanaByteLength; i++)
        {
            int hi = ParseHexNibble(hex[i * 2]);
            int lo = ParseHexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) return false;
            bytes[i] = (byte)((hi << 4) | lo);
        }
        result = FromSolanaBytes(bytes);
        return true;
    }

    public static bool TryParse(string? hex, out Signature result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Signature result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Signature result)
        => TryParse(s, out result);

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the hex representation with 0x prefix.
    /// </summary>
    public override string ToString()
    {
        int hexLen = HexLength;
        return string.Create(hexLen + 2, this, static (chars, sig) =>
        {
            chars[0] = '0';
            chars[1] = 'x';
            sig.FormatHexCore(chars.Slice(2), uppercase: false);
        });
    }

    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";
        bool uppercase = format == "X" || format == "0X";
        bool hasPrefix = format == "0x" || format == "0X" || format.Length == 0;

        int hexLen = HexLength;
        int totalLen = hasPrefix ? hexLen + 2 : hexLen;

        return string.Create(totalLen, (this, uppercase, hasPrefix), static (chars, state) =>
        {
            int pos = 0;
            if (state.hasPrefix)
            {
                chars[0] = '0';
                chars[1] = 'x';
                pos = 2;
            }
            state.Item1.FormatHexCore(chars.Slice(pos), state.uppercase);
        });
    }

    private void FormatHexCore(Span<char> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        int pos = 0;

        FormatUInt64Hex(_u0, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u1, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u2, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u3, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u4, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u5, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u6, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64Hex(_u7, destination.Slice(pos), hexTable); pos += 16;

        if (_type == SignatureType.Evm)
        {
            destination[pos] = (char)hexTable[_v >> 4];
            destination[pos + 1] = (char)hexTable[_v & 0xF];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt64Hex(ulong value, Span<char> destination, ReadOnlySpan<byte> hexTable)
    {
        for (int i = 15; i >= 0; i--)
        {
            destination[i] = (char)hexTable[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int hexLen = HexLength;
        int requiredLength = hasPrefix ? hexLen + 2 : hexLen;

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
        int hexLen = HexLength;
        int requiredLength = hasPrefix ? hexLen + 2 : hexLen;

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

    private void FormatHexCoreUtf8(Span<byte> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        int pos = 0;

        FormatUInt64HexUtf8(_u0, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u1, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u2, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u3, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u4, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u5, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u6, destination.Slice(pos), hexTable); pos += 16;
        FormatUInt64HexUtf8(_u7, destination.Slice(pos), hexTable); pos += 16;

        if (_type == SignatureType.Evm)
        {
            destination[pos] = hexTable[_v >> 4];
            destination[pos + 1] = hexTable[_v & 0xF];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt64HexUtf8(ulong value, Span<byte> destination, ReadOnlySpan<byte> hexTable)
    {
        for (int i = 15; i >= 0; i--)
        {
            destination[i] = hexTable[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Creates a zero EVM signature.
    /// </summary>
    public static Signature ZeroEvm => new(0, 0, 0, 0, 0, 0, 0, 0, 0, SignatureType.Evm);

    /// <summary>
    /// Creates a zero Solana signature.
    /// </summary>
    public static Signature ZeroSolana => new(0, 0, 0, 0, 0, 0, 0, 0, 0, SignatureType.Solana);

    #endregion
}

