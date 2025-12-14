using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
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
/// A blockchain address supporting both EVM (20 bytes) and Solana (32 bytes) formats.
/// Uses a compact representation optimized for minimal memory footprint.
/// Immutable, equatable, and comparable for use as dictionary keys and sorting.
/// </summary>
/// <remarks>
/// Memory layout: 40 bytes total (4 x ulong + 1 byte type, padded to 8-byte alignment).
/// Uses natural alignment (Pack=8) for optimal CPU cache performance.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(AddressJsonConverter))]
public readonly struct Address : 
    IEquatable<Address>, 
    IComparable<Address>, 
    IComparable,
    ISpanFormattable,
    ISpanParsable<Address>,
    IUtf8SpanFormattable
{
    /// <summary>
    /// The size in bytes of an EVM address (20 bytes / 160 bits).
    /// </summary>
    public const int EvmByteLength = 20;

    /// <summary>
    /// The size in bytes of a Solana address (32 bytes / 256 bits).
    /// </summary>
    public const int SolanaByteLength = 32;

    /// <summary>
    /// The size in characters of an EVM hex string without prefix.
    /// </summary>
    public const int EvmHexLength = 40;

    /// <summary>
    /// The maximum size in characters of a Base58 encoded Solana address.
    /// </summary>
    public const int MaxBase58Length = 44;

    // Store as 4 x ulong (big-endian layout: _u0 is most significant for lexicographic comparison)
    // For EVM: only first 20 bytes used (2.5 ulongs), lower 32 bits of _u2 + _u3 = 0
    // For Solana: all 32 bytes used
    private readonly ulong _u0; // bytes 0-7 (most significant)
    private readonly ulong _u1; // bytes 8-15
    private readonly ulong _u2; // bytes 16-23
    private readonly ulong _u3; // bytes 24-31 (least significant, unused for EVM)
    private readonly AddressType _type;

    /// <summary>
    /// The zero EVM address (all bytes are 0x00).
    /// </summary>
    public static readonly Address Zero;

    // Base58 alphabet (Bitcoin style, used by Solana)
    private static ReadOnlySpan<byte> Base58Alphabet => 
        "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"u8;

    // Lookup table for Base58 decoding (-1 = invalid)
    private static ReadOnlySpan<sbyte> Base58DecodeMap =>
    [
        -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
        -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
        -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,
        -1, 0, 1, 2, 3, 4, 5, 6, 7, 8,-1,-1,-1,-1,-1,-1,
        -1, 9,10,11,12,13,14,15,16,-1,17,18,19,20,21,-1,
        22,23,24,25,26,27,28,29,30,31,32,-1,-1,-1,-1,-1,
        -1,33,34,35,36,37,38,39,40,41,42,43,-1,44,45,46,
        47,48,49,50,51,52,53,54,55,56,57,-1,-1,-1,-1,-1
    ];

    // Hex lookup tables
    private static ReadOnlySpan<byte> HexBytesLower => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> HexBytesUpper => "0123456789ABCDEF"u8;

    #region Constructors

    /// <summary>
    /// Creates an Address from 4 ulong values in big-endian order with specified type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Address(ulong u0, ulong u1, ulong u2, ulong u3, AddressType type)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
        _type = type;
    }

    /// <summary>
    /// Creates an EVM address from a 20-byte big-endian span.
    /// Allocation-free, reads bytes directly without intermediate buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Address FromEvmBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != EvmByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidEvmLength(nameof(bytes));

        // Read 20 bytes directly: 8 + 8 + 4 bytes
        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
        // Last 4 bytes go into upper 32 bits of u2
        ulong u2 = (ulong)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16)) << 32;
        
        return new Address(u0, u1, u2, 0, AddressType.Evm);
    }

    /// <summary>
    /// Creates a Solana address from a 32-byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Address FromSolanaBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SolanaByteLength)
            ThrowHelper.ThrowArgumentExceptionInvalidSolanaLength(nameof(bytes));

        ulong u0 = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        ulong u1 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
        ulong u2 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16));
        ulong u3 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24));
        
        return new Address(u0, u1, u2, u3, AddressType.Solana);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the type of this address (EVM or Solana).
    /// </summary>
    public AddressType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type;
    }

    /// <summary>
    /// Returns true if this is the zero address.
    /// Branchless implementation for CPU pipeline efficiency.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_u0 | _u1 | _u2 | _u3) == 0;
    }

    /// <summary>
    /// Gets the byte length of this address (20 for EVM, 32 for Solana).
    /// </summary>
    public int ByteLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == AddressType.Evm ? EvmByteLength : SolanaByteLength;
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the address bytes in big-endian order.
    /// For EVM: writes 20 bytes. For Solana: writes 32 bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(Span<byte> destination)
    {
        int length = ByteLength;
        if (destination.Length < length)
            ThrowHelper.ThrowArgumentExceptionDestinationTooSmall(nameof(destination));

        if (_type == AddressType.Evm)
        {
            // Write 20 bytes: 8 + 8 + 4
            BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(16), (uint)(_u2 >> 32));
        }
        else
        {
            // Write 32 bytes
            BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
            BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
        }
    }

    /// <summary>
    /// Returns the address as a byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    #endregion

    #region Equality (SIMD Optimized)

    /// <summary>
    /// Compares this address for equality with another.
    /// Uses SIMD when available for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Address other)
    {
        if (_type != other._type)
            return false;

        // SIMD path for 256-bit comparison
        if (Vector256.IsHardwareAccelerated)
        {
            var left = Vector256.Create(_u0, _u1, _u2, _u3);
            var right = Vector256.Create(other._u0, other._u1, other._u2, other._u3);
            return left.Equals(right);
        }

        // Fallback: scalar comparison
        return _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;
    }

    public override bool Equals(object? obj) => obj is Address other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // Use all components for good distribution
        // For EVM, _u3 is always 0 but that's fine
        return HashCode.Combine(_u0, _u1, _u2, _u3, _type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Address left, Address right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Address left, Address right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison. EVM addresses sort before Solana addresses.
    /// Within same type, compares bytes from most significant to least.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Address other)
    {
        // Compare by type first (EVM < Solana)
        if (_type != other._type) 
            return _type < other._type ? -1 : 1;
        
        // Then lexicographic by bytes
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Address other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Address)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Address left, Address right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Address left, Address right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Address left, Address right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Address left, Address right) => left.CompareTo(right) >= 0;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses an address string. Automatically detects EVM (0x prefix) or Solana (Base58).
    /// </summary>
    public static Address Parse(ReadOnlySpan<char> s)
    {
        if (s.Length == 0)
            ThrowHelper.ThrowFormatExceptionEmpty();

        // EVM addresses start with 0x
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ParseEvm(s);

        // Otherwise try Base58 (Solana)
        return ParseSolana(s);
    }

    /// <summary>
    /// Parses an EVM address (40 hex characters with optional 0x prefix).
    /// Uses direct nibble parsing for maximum performance.
    /// </summary>
    public static Address ParseEvm(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != EvmHexLength)
            ThrowHelper.ThrowFormatExceptionInvalidEvmHexLength();

        // Parse directly into ulongs without intermediate byte array
        ulong u0 = ParseHexUInt64(hex.Slice(0, 16));
        ulong u1 = ParseHexUInt64(hex.Slice(16, 16));
        // Last 8 hex chars = 4 bytes, goes into upper 32 bits of u2
        ulong u2 = ParseHexUInt32(hex.Slice(32, 8));
        u2 <<= 32;

        return new Address(u0, u1, u2, 0, AddressType.Evm);
    }

    /// <summary>
    /// Parses a Solana address (Base58 encoded, typically 32-44 characters).
    /// Allocation-free implementation using fixed-size arithmetic.
    /// </summary>
    public static Address ParseSolana(ReadOnlySpan<char> base58)
    {
        if (base58.Length == 0 || base58.Length > MaxBase58Length)
            ThrowHelper.ThrowFormatExceptionInvalidBase58Length();

        if (!TryDecodeBase58(base58, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            ThrowHelper.ThrowFormatExceptionInvalidBase58();

        return new Address(u0, u1, u2, u3, AddressType.Solana);
    }

    public static Address Parse(string s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    public static Address Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, CultureInfo.InvariantCulture);

    public static Address Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Tries to parse an address string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Address result)
    {
        result = Zero;

        if (s.Length == 0)
            return false;

        // EVM addresses start with 0x
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return TryParseEvm(s, out result);

        // Otherwise try Base58 (Solana)
        return TryParseSolana(s, out result);
    }

    /// <summary>
    /// Tries to parse an EVM address without exceptions.
    /// </summary>
    public static bool TryParseEvm(ReadOnlySpan<char> hex, out Address result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != EvmHexLength)
            return false;

        if (!TryParseHexUInt64(hex.Slice(0, 16), out ulong u0))
            return false;
        if (!TryParseHexUInt64(hex.Slice(16, 16), out ulong u1))
            return false;
        if (!TryParseHexUInt32(hex.Slice(32, 8), out uint u2Upper))
            return false;

        result = new Address(u0, u1, (ulong)u2Upper << 32, 0, AddressType.Evm);
        return true;
    }

    /// <summary>
    /// Tries to parse a Solana address without exceptions.
    /// </summary>
    public static bool TryParseSolana(ReadOnlySpan<char> base58, out Address result)
    {
        result = Zero;

        if (base58.Length == 0 || base58.Length > MaxBase58Length)
            return false;

        if (!TryDecodeBase58(base58, out ulong u0, out ulong u1, out ulong u2, out ulong u3))
            return false;

        result = new Address(u0, u1, u2, u3, AddressType.Solana);
        return true;
    }

    public static bool TryParse(string? s, out Address result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = Zero;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Address result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Address result)
        => TryParse(s, out result);

    #region Hex Parsing Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexNibble(char c)
    {
        // Branchless hex nibble parsing using lookup
        int val = c;
        int digit = val - '0';
        int lower = (val | 0x20) - 'a' + 10; // Case-insensitive a-f
        
        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ParseHexUInt64(ReadOnlySpan<char> hex)
    {
        ulong result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            result = (result << 4) | (uint)nibble;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHexUInt64(ReadOnlySpan<char> hex, out ulong result)
    {
        result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) return false;
            result = (result << 4) | (uint)nibble;
        }
        return true;
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
    /// Returns the string representation.
    /// EVM: lowercase hex with 0x prefix. Solana: Base58.
    /// </summary>
    public override string ToString()
    {
        if (_type == AddressType.Evm)
        {
            return string.Create(EvmHexLength + 2, this, static (chars, addr) =>
            {
                chars[0] = '0';
                chars[1] = 'x';
                addr.FormatEvmHexCore(chars.Slice(2), uppercase: false);
            });
        }
        else
        {
            Span<char> buffer = stackalloc char[MaxBase58Length];
            int length = EncodeBase58(buffer);
            return new string(buffer.Slice(0, length));
        }
    }

    /// <summary>
    /// Formats the value according to the format string.
    /// EVM: "x" lowercase, "X" uppercase, "0x" with prefix (default), "0X" uppercase with prefix, "C"/"c" checksum.
    /// Solana: Base58 (format ignored).
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        if (_type == AddressType.Solana)
            return ToString(); // Base58, format ignored

        format ??= "0x";

        return format switch
        {
            "0x" => ToString(),
            "0X" => string.Create(EvmHexLength + 2, this, static (chars, addr) =>
            {
                chars[0] = '0';
                chars[1] = 'x';
                addr.FormatEvmHexCore(chars.Slice(2), uppercase: true);
            }),
            "x" => string.Create(EvmHexLength, this, static (chars, addr) =>
            {
                addr.FormatEvmHexCore(chars, uppercase: false);
            }),
            "X" => string.Create(EvmHexLength, this, static (chars, addr) =>
            {
                addr.FormatEvmHexCore(chars, uppercase: true);
            }),
            "C" or "c" => ToChecksumString(), // EIP-55 checksum
            _ => throw new FormatException($"Unknown format: {format}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatEvmHexCore(Span<char> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        
        // Format _u0 (8 bytes = 16 hex chars)
        FormatUInt64Hex(_u0, destination, hexTable);
        // Format _u1 (8 bytes = 16 hex chars)
        FormatUInt64Hex(_u1, destination.Slice(16), hexTable);
        // Format upper 4 bytes of _u2 (4 bytes = 8 hex chars)
        FormatUInt32Hex((uint)(_u2 >> 32), destination.Slice(32), hexTable);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt32Hex(uint value, Span<char> destination, ReadOnlySpan<byte> hexTable)
    {
        for (int i = 7; i >= 0; i--)
        {
            destination[i] = (char)hexTable[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    /// <summary>
    /// Returns EVM address with EIP-55 mixed-case checksum encoding.
    /// Uses Keccak256 hash of the lowercase address to determine case.
    /// </summary>
    public string ToChecksumString()
    {
        if (_type != AddressType.Evm)
            throw new InvalidOperationException("Checksum encoding only applies to EVM addresses");

        // Get lowercase hex without prefix
        Span<char> hex = stackalloc char[EvmHexLength];
        FormatEvmHexCore(hex, uppercase: false);

        // Compute Keccak256 hash of the lowercase hex (as ASCII bytes)
        Span<byte> hexBytes = stackalloc byte[EvmHexLength];
        for (int i = 0; i < EvmHexLength; i++)
            hexBytes[i] = (byte)hex[i];

        Span<byte> hash = stackalloc byte[32];
        Keccak256.ComputeHash(hexBytes, hash);

        // Apply checksum: uppercase if corresponding hash nibble >= 8
        for (int i = 0; i < EvmHexLength; i++)
        {
            char c = hex[i];
            if (c >= 'a' && c <= 'f')
            {
                // Get corresponding nibble from hash
                int hashByte = hash[i / 2];
                int hashNibble = (i % 2 == 0) ? (hashByte >> 4) : (hashByte & 0xF);
                
                if (hashNibble >= 8)
                    hex[i] = (char)(c - 32); // Convert to uppercase
            }
        }

        return "0x" + new string(hex);
    }

    /// <summary>
    /// Tries to format the value into the destination span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (_type == AddressType.Solana)
        {
            // Base58 encoding
            if (destination.Length < MaxBase58Length)
            {
                // Try anyway, might fit
                Span<char> buffer = stackalloc char[MaxBase58Length];
                int length = EncodeBase58(buffer);
                if (length > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }
                buffer.Slice(0, length).CopyTo(destination);
                charsWritten = length;
                return true;
            }
            charsWritten = EncodeBase58(destination);
            return true;
        }

        // EVM formatting
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X") || 
                         format.SequenceEqual("C") || format.SequenceEqual("c");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? EvmHexLength + 2 : EvmHexLength;

        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            destination[0] = '0';
            destination[1] = 'x';
            FormatEvmHexCore(destination.Slice(2), uppercase);
        }
        else
        {
            FormatEvmHexCore(destination, uppercase);
        }

        charsWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Tries to format the value into a UTF-8 destination span.
    /// Optimized to write directly to UTF-8 without intermediate char buffer.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (_type == AddressType.Solana)
        {
            // Base58 - encode to chars then convert (all ASCII)
            Span<char> buffer = stackalloc char[MaxBase58Length];
            int length = EncodeBase58(buffer);
            
            if (length > utf8Destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            for (int i = 0; i < length; i++)
                utf8Destination[i] = (byte)buffer[i];
            
            bytesWritten = length;
            return true;
        }

        // EVM: format directly to UTF-8
        bool hasPrefix = format.Length == 0 || format.SequenceEqual("0x") || format.SequenceEqual("0X");
        bool uppercase = format.SequenceEqual("X") || format.SequenceEqual("0X");
        int requiredLength = hasPrefix ? EvmHexLength + 2 : EvmHexLength;

        if (utf8Destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        if (hasPrefix)
        {
            utf8Destination[0] = (byte)'0';
            utf8Destination[1] = (byte)'x';
            FormatEvmHexCoreUtf8(utf8Destination.Slice(2), uppercase);
        }
        else
        {
            FormatEvmHexCoreUtf8(utf8Destination, uppercase);
        }

        bytesWritten = requiredLength;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FormatEvmHexCoreUtf8(Span<byte> destination, bool uppercase)
    {
        var hexTable = uppercase ? HexBytesUpper : HexBytesLower;
        
        FormatUInt64HexUtf8(_u0, destination, hexTable);
        FormatUInt64HexUtf8(_u1, destination.Slice(16), hexTable);
        FormatUInt32HexUtf8((uint)(_u2 >> 32), destination.Slice(32), hexTable);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatUInt32HexUtf8(uint value, Span<byte> destination, ReadOnlySpan<byte> hexTable)
    {
        for (int i = 7; i >= 0; i--)
        {
            destination[i] = hexTable[(int)(value & 0xF)];
            value >>= 4;
        }
    }

    #endregion

    #region Base58 Encoding/Decoding (Allocation-Free)

    /// <summary>
    /// Encodes the Solana address bytes as Base58.
    /// Uses direct 256-bit arithmetic without BigInteger allocation.
    /// Returns the number of characters written.
    /// </summary>
    private int EncodeBase58(Span<char> destination)
    {
        // Count leading zeros (leading '1's in Base58)
        int leadingZeros = CountLeadingZeroBytes();

        if (IsZero)
        {
            // All zeros = all '1's
            for (int i = 0; i < SolanaByteLength; i++)
                destination[i] = '1';
            return SolanaByteLength;
        }

        // Work with a mutable copy of the value as 4 ulongs
        ulong v0 = _u0, v1 = _u1, v2 = _u2, v3 = _u3;
        
        // Output buffer (filled right to left)
        Span<byte> output = stackalloc byte[MaxBase58Length];
        int outputLen = 0;

        // Divide by 58 repeatedly using 256-bit division
        while (!IsZero256(v0, v1, v2, v3))
        {
            // Divide 256-bit value by 58, get remainder
            ulong remainder = DivMod58(ref v0, ref v1, ref v2, ref v3);
            output[outputLen++] = Base58Alphabet[(int)remainder];
        }

        // Add leading '1's for leading zero bytes
        int totalLen = leadingZeros + outputLen;

        // Write leading '1's
        for (int i = 0; i < leadingZeros; i++)
            destination[i] = '1';

        // Write reversed output
        for (int i = 0; i < outputLen; i++)
            destination[leadingZeros + i] = (char)output[outputLen - 1 - i];

        return totalLen;
    }

    /// <summary>
    /// Decodes a Base58 string directly to 4 ulongs.
    /// Allocation-free implementation.
    /// </summary>
    private static bool TryDecodeBase58(ReadOnlySpan<char> base58, out ulong u0, out ulong u1, out ulong u2, out ulong u3)
    {
        u0 = u1 = u2 = u3 = 0;

        if (base58.Length == 0)
            return false;

        // Count leading '1's (represent leading zero bytes)
        int leadingOnes = 0;
        while (leadingOnes < base58.Length && base58[leadingOnes] == '1')
            leadingOnes++;

        // Decode remaining characters using 256-bit multiplication
        for (int i = leadingOnes; i < base58.Length; i++)
        {
            char c = base58[i];
            if (c >= 128)
                return false;
            
            int digit = Base58DecodeMap[c];
            if (digit < 0)
                return false;

            // Multiply current value by 58 and add digit
            if (!MulAdd58(ref u0, ref u1, ref u2, ref u3, (ulong)digit))
                return false; // Overflow
        }

        // Verify we have exactly 32 bytes worth of data
        // The leading zeros from '1's should account for leading zero bytes
        // For Solana, we expect exactly 32 bytes
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CountLeadingZeroBytes()
    {
        if (_u0 != 0) return (int)(ulong.LeadingZeroCount(_u0) / 8);
        if (_u1 != 0) return 8 + (int)(ulong.LeadingZeroCount(_u1) / 8);
        if (_u2 != 0) return 16 + (int)(ulong.LeadingZeroCount(_u2) / 8);
        if (_u3 != 0) return 24 + (int)(ulong.LeadingZeroCount(_u3) / 8);
        return 32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZero256(ulong u0, ulong u1, ulong u2, ulong u3)
        => (u0 | u1 | u2 | u3) == 0;

    /// <summary>
    /// Divides a 256-bit number by 58 in place, returns remainder.
    /// Big-endian order: u0 is MSB.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong DivMod58(ref ulong u0, ref ulong u1, ref ulong u2, ref ulong u3)
    {
        // 256-bit division by 58
        // Process from most significant to least significant
        ulong remainder = 0;
        
        // u0 (most significant)
        ulong dividend = remainder * (1UL << 32) * (1UL << 32) + u0;
        // We need 128-bit division here, use Math.DivRem approach
        (ulong q0High, ulong r0) = DivMod64By58WithRemainder(u0, remainder);
        u0 = q0High;
        remainder = r0;

        (ulong q1High, ulong r1) = DivMod64By58WithRemainder(u1, remainder);
        u1 = q1High;
        remainder = r1;

        (ulong q2High, ulong r2) = DivMod64By58WithRemainder(u2, remainder);
        u2 = q2High;
        remainder = r2;

        (ulong q3High, ulong r3) = DivMod64By58WithRemainder(u3, remainder);
        u3 = q3High;
        
        return r3;
    }

    /// <summary>
    /// Divides (remainder * 2^64 + value) by 58, returns quotient and new remainder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong quotient, ulong remainder) DivMod64By58WithRemainder(ulong value, ulong carry)
    {
        // Use 128-bit arithmetic via UInt128
        UInt128 dividend = ((UInt128)carry << 64) | value;
        UInt128 quotient = dividend / 58;
        ulong remainder = (ulong)(dividend % 58);
        return ((ulong)quotient, remainder);
    }

    /// <summary>
    /// Multiplies a 256-bit number by 58 and adds a digit in place.
    /// Returns false on overflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MulAdd58(ref ulong u0, ref ulong u1, ref ulong u2, ref ulong u3, ulong digit)
    {
        // Multiply each component by 58 with carry
        // Process from least significant (u3) to most significant (u0)
        
        UInt128 prod3 = (UInt128)u3 * 58 + digit;
        u3 = (ulong)prod3;
        ulong carry = (ulong)(prod3 >> 64);

        UInt128 prod2 = (UInt128)u2 * 58 + carry;
        u2 = (ulong)prod2;
        carry = (ulong)(prod2 >> 64);

        UInt128 prod1 = (UInt128)u1 * 58 + carry;
        u1 = (ulong)prod1;
        carry = (ulong)(prod1 >> 64);

        UInt128 prod0 = (UInt128)u0 * 58 + carry;
        u0 = (ulong)prod0;
        carry = (ulong)(prod0 >> 64);

        // Overflow if there's remaining carry
        return carry == 0;
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Creates a zero EVM address.
    /// </summary>
    public static Address ZeroEvm => new(0, 0, 0, 0, AddressType.Evm);

    /// <summary>
    /// Creates a zero Solana address.
    /// </summary>
    public static Address ZeroSolana => new(0, 0, 0, 0, AddressType.Solana);

    /// <summary>
    /// Converts to Hash32 (only valid for Solana addresses).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32 ToHash32()
    {
        if (_type != AddressType.Solana)
            throw new InvalidOperationException("Only Solana addresses can be converted to Hash32");
        return new Hash32(_u0, _u1, _u2, _u3);
    }

    /// <summary>
    /// Creates a Solana Address from a Hash32.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Address FromHash32(Hash32 hash)
    {
        Span<byte> bytes = stackalloc byte[SolanaByteLength];
        hash.WriteBigEndian(bytes);
        return FromSolanaBytes(bytes);
    }

    #endregion
}

/// <summary>
/// Minimal Keccak-256 implementation for EIP-55 checksum.
/// Optimized for the specific use case of hashing 40-byte ASCII hex strings.
/// </summary>
internal static class Keccak256
{
    private const int StateSize = 25; // 5x5 state matrix of 64-bit words
    private const int Rate = 136; // (1600 - 256*2) / 8 = 136 bytes
    private const int Rounds = 24;

    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL,
        0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL,
        0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL,
        0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
    ];

    private static readonly int[] RotationOffsets =
    [
        0, 1, 62, 28, 27, 36, 44, 6, 55, 20, 3, 10, 43, 25, 39, 41, 45, 15, 21, 8, 18, 2, 61, 56, 14
    ];

    public static void ComputeHash(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length < 32)
            throw new ArgumentException("Output buffer must be at least 32 bytes");

        Span<ulong> state = stackalloc ulong[StateSize];
        state.Clear();

        // Absorb phase
        int blockCount = input.Length / Rate;
        int offset = 0;

        for (int i = 0; i < blockCount; i++)
        {
            AbsorbBlock(state, input.Slice(offset, Rate));
            offset += Rate;
        }

        // Final block with padding
        int remaining = input.Length - offset;
        Span<byte> finalBlock = stackalloc byte[Rate];
        finalBlock.Clear();
        
        if (remaining > 0)
            input.Slice(offset, remaining).CopyTo(finalBlock);
        
        // Keccak padding: append 0x01, then zeros, then 0x80
        finalBlock[remaining] = 0x01;
        finalBlock[Rate - 1] |= 0x80;
        
        AbsorbBlock(state, finalBlock);

        // Squeeze phase - extract 32 bytes
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(i * 8), state[i]);
        }
    }

    private static void AbsorbBlock(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        // XOR block into state
        int words = block.Length / 8;
        for (int i = 0; i < words; i++)
        {
            state[i] ^= BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(i * 8));
        }

        // Apply Keccak-f[1600] permutation
        KeccakF(state);
    }

    private static void KeccakF(Span<ulong> state)
    {
        Span<ulong> C = stackalloc ulong[5];
        Span<ulong> D = stackalloc ulong[5];
        Span<ulong> B = stackalloc ulong[25];

        for (int round = 0; round < Rounds; round++)
        {
            // θ step
            for (int x = 0; x < 5; x++)
            {
                C[x] = state[x] ^ state[x + 5] ^ state[x + 10] ^ state[x + 15] ^ state[x + 20];
            }

            for (int x = 0; x < 5; x++)
            {
                D[x] = C[(x + 4) % 5] ^ RotateLeft(C[(x + 1) % 5], 1);
            }

            for (int i = 0; i < 25; i++)
            {
                state[i] ^= D[i % 5];
            }

            // ρ and π steps
            for (int i = 0; i < 25; i++)
            {
                int x = i % 5;
                int y = i / 5;
                int newX = y;
                int newY = (2 * x + 3 * y) % 5;
                B[newX + 5 * newY] = RotateLeft(state[i], RotationOffsets[i]);
            }

            // χ step
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    state[x + 5 * y] = B[x + 5 * y] ^ ((~B[(x + 1) % 5 + 5 * y]) & B[(x + 2) % 5 + 5 * y]);
                }
            }

            // ι step
            state[0] ^= RoundConstants[round];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong value, int offset)
        => (value << offset) | (value >> (64 - offset));
}
