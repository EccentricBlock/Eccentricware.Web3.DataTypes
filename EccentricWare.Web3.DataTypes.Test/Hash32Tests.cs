using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes;

/// <summary>
/// A 32-byte (256-bit) hash optimized for EVM and Solana blockchain operations.
/// Uses 4 x 64-bit unsigned integers for minimal memory footprint (32 bytes).
/// Immutable, equatable, and comparable for use as dictionary keys and sorting.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Hash32JsonConverter))]
public readonly struct Hash32 : 
    IEquatable<Hash32>, 
    IComparable<Hash32>, 
    IComparable,
    ISpanFormattable,
    ISpanParsable<Hash32>
{
    // Store as 4 x ulong (big-endian layout: _u0 is most significant for lexicographic comparison)
    private readonly ulong _u0; // bytes 0-7 (most significant)
    private readonly ulong _u1; // bytes 8-15
    private readonly ulong _u2; // bytes 16-23
    private readonly ulong _u3; // bytes 24-31 (least significant)

    /// <summary>
    /// The zero hash (all bytes are 0x00).
    /// </summary>
    public static readonly Hash32 Zero = default;

    #region Constructors

    /// <summary>
    /// Creates a Hash32 from 4 ulong values in big-endian order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Hash32(ulong u0, ulong u1, ulong u2, ulong u3)
    {
        _u0 = u0;
        _u1 = u1;
        _u2 = u2;
        _u3 = u3;
    }

    /// <summary>
    /// Creates a Hash32 from a 32-byte big-endian span.
    /// Compatible with EVM transaction/block hashes.
    /// </summary>
    public Hash32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32)
            throw new ArgumentException("Hash32 requires exactly 32 bytes", nameof(bytes));

        _u0 = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        _u1 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8));
        _u2 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(16));
        _u3 = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(24));
    }

    /// <summary>
    /// Creates a Hash32 from a little-endian byte span.
    /// Compatible with Solana encoding.
    /// </summary>
    public static Hash32 FromLittleEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32)
            throw new ArgumentException("Hash32 requires exactly 32 bytes", nameof(bytes));

        ulong u3 = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        ulong u2 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8));
        ulong u1 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16));
        ulong u0 = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(24));
        return new Hash32(u0, u1, u2, u3);
    }

    #endregion

    #region Byte Conversions

    /// <summary>
    /// Writes the hash as a 32-byte big-endian span.
    /// Compatible with EVM encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBigEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes", nameof(destination));

        BinaryPrimitives.WriteUInt64BigEndian(destination, _u0);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8), _u1);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16), _u2);
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(24), _u3);
    }

    /// <summary>
    /// Writes the hash as a 32-byte little-endian span.
    /// Compatible with Solana encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLittleEndian(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _u3);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8), _u2);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16), _u1);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24), _u0);
    }

    /// <summary>
    /// Returns the hash as a 32-byte big-endian array.
    /// </summary>
    public byte[] ToBigEndianBytes()
    {
        var bytes = new byte[32];
        WriteBigEndian(bytes);
        return bytes;
    }

    /// <summary>
    /// Returns the hash as a 32-byte little-endian array.
    /// </summary>
    public byte[] ToLittleEndianBytes()
    {
        var bytes = new byte[32];
        WriteLittleEndian(bytes);
        return bytes;
    }

    #endregion

    #region Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hash32 other) =>
        _u0 == other._u0 && _u1 == other._u1 && _u2 == other._u2 && _u3 == other._u3;

    public override bool Equals(object? obj) => obj is Hash32 other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hash32 left, Hash32 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hash32 left, Hash32 right) => !left.Equals(right);

    #endregion

    #region Comparison

    /// <summary>
    /// Lexicographic comparison (most significant to least significant bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Hash32 other)
    {
        if (_u0 != other._u0) return _u0 < other._u0 ? -1 : 1;
        if (_u1 != other._u1) return _u1 < other._u1 ? -1 : 1;
        if (_u2 != other._u2) return _u2 < other._u2 ? -1 : 1;
        if (_u3 != other._u3) return _u3 < other._u3 ? -1 : 1;
        return 0;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is Hash32 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Hash32)}", nameof(obj));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Hash32 left, Hash32 right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Hash32 left, Hash32 right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Hash32 left, Hash32 right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Hash32 left, Hash32 right) => left.CompareTo(right) >= 0;

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a 64-character hexadecimal string (with or without 0x prefix).
    /// Allocation-free using span-based parsing.
    /// </summary>
    public static Hash32 Parse(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != 64)
            throw new FormatException("Hash32 requires exactly 64 hex characters (32 bytes)");

        if (!ulong.TryParse(hex.Slice(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u0))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(hex.Slice(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u1))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(hex.Slice(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u2))
            throw new FormatException("Invalid hex string");
        if (!ulong.TryParse(hex.Slice(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u3))
            throw new FormatException("Invalid hex string");

        return new Hash32(u0, u1, u2, u3);
    }

    public static Hash32 Parse(string hex) => Parse(hex.AsSpan());

    public static Hash32 Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);

    public static Hash32 Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan());

    /// <summary>
    /// Tries to parse a hexadecimal string without exceptions.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out Hash32 result)
    {
        result = Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Slice(2);

        if (hex.Length != 64)
            return false;

        if (!ulong.TryParse(hex.Slice(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u0))
            return false;
        if (!ulong.TryParse(hex.Slice(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u1))
            return false;
        if (!ulong.TryParse(hex.Slice(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u2))
            return false;
        if (!ulong.TryParse(hex.Slice(48, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u3))
            return false;

        result = new Hash32(u0, u1, u2, u3);
        return true;
    }

    public static bool TryParse(string? hex, out Hash32 result)
    {
        if (string.IsNullOrEmpty(hex))
        {
            result = Zero;
            return false;
        }
        return TryParse(hex.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Hash32 result)
        => TryParse(s, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Hash32 result)
        => TryParse(s, out result);

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the hexadecimal representation with 0x prefix (lowercase).
    /// </summary>
    public override string ToString() => $"0x{_u0:x16}{_u1:x16}{_u2:x16}{_u3:x16}";

    /// <summary>
    /// Formats the value according to the format string.
    /// "x" for lowercase hex, "X" for uppercase hex, "0x" for lowercase with prefix (default).
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format ??= "0x";

        return format switch
        {
            "0x" => $"0x{_u0:x16}{_u1:x16}{_u2:x16}{_u3:x16}",
            "0X" => $"0x{_u0:X16}{_u1:X16}{_u2:X16}{_u3:X16}",
            "x" => $"{_u0:x16}{_u1:x16}{_u2:x16}{_u3:x16}",
            "X" => $"{_u0:X16}{_u1:X16}{_u2:X16}{_u3:X16}",
            _ => throw new FormatException($"Unknown format: {format}")
        };
    }

    /// <summary>
    /// Tries to format the value into the destination span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        var str = ToString(format.Length == 0 ? null : new string(format), provider);
        if (str.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        str.AsSpan().CopyTo(destination);
        charsWritten = str.Length;
        return true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if this is the zero hash.
    /// </summary>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0 == 0 && _u1 == 0 && _u2 == 0 && _u3 == 0;
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Explicit conversion from byte array.
    /// </summary>
    public static explicit operator Hash32(byte[] bytes) => new(bytes);

    /// <summary>
    /// Converts to uint256 for numeric operations if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint256 ToUInt256() => new(_u3, _u2, _u1, _u0);

    /// <summary>
    /// Creates a Hash32 from a uint256.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Hash32 FromUInt256(uint256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.WriteBigEndian(bytes);
        return new Hash32(bytes);
    }

    #endregion
}

/// <summary>
/// JSON converter for Hash32 that serializes as hex string with 0x prefix.
/// </summary>
public sealed class Hash32JsonConverter : JsonConverter<Hash32>
{
    public override Hash32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (str is null)
                return Hash32.Zero;

            return Hash32.Parse(str);
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to Hash32");
    }

    public override void Write(Utf8JsonWriter writer, Hash32 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
```

---

## `Hash32Tests.cs`

```csharp:EccentricWare.Web3.DataTypes.Test/Hash32Tests.cs
using EccentricWare.Web3.DataTypes;

namespace EccentricWare.Web3.DataTypes.Test;

[TestClass]
public sealed class Hash32Tests
{
    private const string TestHashHex = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ZeroHashHex = "0x0000000000000000000000000000000000000000000000000000000000000000";

    #region Construction Tests

    [TestMethod]
    public void Constructor_Default_IsZero()
    {
        Hash32 value = default;
        Assert.IsTrue(value.IsZero);
        Assert.AreEqual(Hash32.Zero, value);
    }

    [TestMethod]
    public void Constructor_FromFourUlongs_CorrectValue()
    {
        Hash32 value = new(0x1234567890abcdef, 0x1234567890abcdef, 0x1234567890abcdef, 0x1234567890abcdef);
        Assert.IsFalse(value.IsZero);
        Assert.AreEqual(TestHashHex, value.ToString());
    }

    [TestMethod]
    public void Constructor_FromBytes_CorrectValue()
    {
        byte[] bytes = new byte[32];
        bytes[0] = 0x42;
        bytes[31] = 0xFF;

        Hash32 value = new(bytes);
        Assert.IsFalse(value.IsZero);
    }

    [TestMethod]
    public void Constructor_FromBytes_RoundTrip()
    {
        byte[] original = new byte[32];
        for (int i = 0; i < 32; i++)
            original[i] = (byte)i;

        Hash32 hash = new(original);
        byte[] result = hash.ToBigEndianBytes();

        CollectionAssert.AreEqual(original, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_FromBytes_WrongLength_Throws()
    {
        byte[] bytes = new byte[16];
        _ = new Hash32(bytes);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_FromBytes_TooLong_Throws()
    {
        byte[] bytes = new byte[64];
        _ = new Hash32(bytes);
    }

    #endregion

    #region Equality Tests

    [TestMethod]
    public void Equals_SameValue_ReturnsTrue()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        Hash32 b = Hash32.Parse(TestHashHex);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        Hash32 b = Hash32.Zero;

        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Equals_Object_CorrectBehavior()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        object b = Hash32.Parse(TestHashHex);
        object c = "not a Hash32";

        Assert.IsTrue(a.Equals(b));
        Assert.IsFalse(a.Equals(c));
        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void GetHashCode_EqualValues_SameHash()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        Hash32 b = Hash32.Parse(TestHashHex);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void GetHashCode_DifferentValues_DifferentHash()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        Hash32 b = Hash32.Zero;

        Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<Hash32, string>();
        Hash32 hash = Hash32.Parse(TestHashHex);

        dict[hash] = "test";
        Assert.AreEqual("test", dict[hash]);

        Hash32 sameHash = Hash32.Parse(TestHashHex);
        Assert.IsTrue(dict.ContainsKey(sameHash));
    }

    #endregion

    #region Comparison Tests

    [TestMethod]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        Hash32 smaller = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");
        Hash32 larger = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000002");

        Assert.IsTrue(smaller.CompareTo(larger) < 0);
        Assert.IsTrue(smaller < larger);
        Assert.IsTrue(smaller <= larger);
    }

    [TestMethod]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        Hash32 smaller = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");
        Hash32 larger = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000002");

        Assert.IsTrue(larger.CompareTo(smaller) > 0);
        Assert.IsTrue(larger > smaller);
        Assert.IsTrue(larger >= smaller);
    }

    [TestMethod]
    public void CompareTo_Equal_ReturnsZero()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        Hash32 b = Hash32.Parse(TestHashHex);

        Assert.AreEqual(0, a.CompareTo(b));
        Assert.IsTrue(a <= b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_LexicographicOrder()
    {
        // Most significant bytes differ
        Hash32 smaller = Hash32.Parse("0x0fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        Hash32 larger = Hash32.Parse("0xf000000000000000000000000000000000000000000000000000000000000000");

        Assert.IsTrue(smaller < larger);
    }

    [TestMethod]
    public void CompareTo_Object_CorrectBehavior()
    {
        Hash32 a = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000001");
        object b = Hash32.Parse("0x0000000000000000000000000000000000000000000000000000000000000002");

        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(a.CompareTo(null) > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void CompareTo_InvalidObject_Throws()
    {
        Hash32 a = Hash32.Parse(TestHashHex);
        a.CompareTo("invalid");
    }

    [TestMethod]
    public void Sorting_Works()
    {
        Hash32[] hashes =
        [
            Hash32.Parse("0x3000000000000000000000000000000000000000000000000000000000000000"),
            Hash32.Parse("0x1000000000000000000000000000000000000000000000000000000000000000"),
            Hash32.Parse("0x2000000000000000000000000000000000000000000000000000000000000000"),
            Hash32.Zero
        ];

        Array.Sort(hashes);

        Assert.AreEqual(Hash32.Zero, hashes[0]);
        Assert.AreEqual(Hash32.Parse("0x1000000000000000000000000000000000000000000000000000000000000000"), hashes[1]);
        Assert.AreEqual(Hash32.Parse("0x2000000000000000000000000000000000000000000000000000000000000000"), hashes[2]);
        Assert.AreEqual(Hash32.Parse("0x3000000000000000000000000000000000000000000000000000000000000000"), hashes[3]);
    }

    #endregion

    #region Parse Tests

    [TestMethod]
    public void Parse_HexWithPrefix_CorrectValue()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        Assert.AreEqual(TestHashHex, value.ToString());
    }

    [TestMethod]
    public void Parse_HexWithoutPrefix_CorrectValue()
    {
        Hash32 value = Hash32.Parse("1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        Assert.AreEqual(TestHashHex, value.ToString());
    }

    [TestMethod]
    public void Parse_UppercaseHex_CorrectValue()
    {
        Hash32 value = Hash32.Parse("0x1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF");
        Assert.AreEqual(TestHashHex, value.ToString());
    }

    [TestMethod]
    public void Parse_ZeroHash_CorrectValue()
    {
        Hash32 value = Hash32.Parse(ZeroHashHex);
        Assert.AreEqual(Hash32.Zero, value);
        Assert.IsTrue(value.IsZero);
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void Parse_TooShort_Throws()
    {
        Hash32.Parse("0x1234");
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void Parse_TooLong_Throws()
    {
        Hash32.Parse("0x" + new string('a', 66));
    }

    [TestMethod]
    [ExpectedException(typeof(FormatException))]
    public void Parse_InvalidCharacters_Throws()
    {
        Hash32.Parse("0xgggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg");
    }

    [TestMethod]
    public void TryParse_Valid_ReturnsTrue()
    {
        bool success = Hash32.TryParse(TestHashHex, out Hash32 result);
        Assert.IsTrue(success);
        Assert.AreEqual(TestHashHex, result.ToString());
    }

    [TestMethod]
    public void TryParse_Invalid_ReturnsFalse()
    {
        bool success = Hash32.TryParse("not-a-hash", out Hash32 result);
        Assert.IsFalse(success);
        Assert.AreEqual(Hash32.Zero, result);
    }

    [TestMethod]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(Hash32.TryParse(null, out _));
        Assert.IsFalse(Hash32.TryParse("", out _));
    }

    [TestMethod]
    public void TryParse_WrongLength_ReturnsFalse()
    {
        Assert.IsFalse(Hash32.TryParse("0x1234", out _));
    }

    #endregion

    #region Byte Conversion Tests

    [TestMethod]
    public void ToBigEndianBytes_CorrectBytes()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        byte[] bytes = value.ToBigEndianBytes();

        Assert.AreEqual(32, bytes.Length);
        Assert.AreEqual(0x12, bytes[0]);
        Assert.AreEqual(0xef, bytes[31]);
    }

    [TestMethod]
    public void ToBigEndianBytes_Zero_AllZeros()
    {
        byte[] bytes = Hash32.Zero.ToBigEndianBytes();

        foreach (byte b in bytes)
            Assert.AreEqual(0, b);
    }

    [TestMethod]
    public void WriteBigEndian_RoundTrip_PreservesValue()
    {
        Hash32 original = Hash32.Parse(TestHashHex);
        byte[] bytes = original.ToBigEndianBytes();
        Hash32 restored = new(bytes);

        Assert.AreEqual(original, restored);
    }

    [TestMethod]
    public void ToLittleEndianBytes_ReverseOfBigEndian()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        byte[] bigEndian = value.ToBigEndianBytes();
        byte[] littleEndian = value.ToLittleEndianBytes();

        Array.Reverse(bigEndian);
        CollectionAssert.AreEqual(bigEndian, littleEndian);
    }

    [TestMethod]
    public void FromLittleEndian_RoundTrip()
    {
        byte[] original = new byte[32];
        for (int i = 0; i < 32; i++)
            original[i] = (byte)i;

        Hash32 hash = Hash32.FromLittleEndian(original);
        byte[] result = hash.ToLittleEndianBytes();

        CollectionAssert.AreEqual(original, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void WriteBigEndian_DestinationTooSmall_Throws()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        byte[] small = new byte[16];
        value.WriteBigEndian(small);
    }

    #endregion

    #region ToString Tests

    [TestMethod]
    public void ToString_ReturnsFullHexWithPrefix()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        string result = value.ToString();

        Assert.AreEqual(66, result.Length); // 0x + 64 hex chars
        Assert.IsTrue(result.StartsWith("0x"));
    }

    [TestMethod]
    public void ToString_Zero_ReturnsFullZeroHash()
    {
        Assert.AreEqual(ZeroHashHex, Hash32.Zero.ToString());
    }

    [TestMethod]
    public void ToString_Format_x_LowercaseNoPrefix()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        string result = value.ToString("x");

        Assert.AreEqual(64, result.Length);
        Assert.IsFalse(result.StartsWith("0x"));
        Assert.AreEqual(result, result.ToLowerInvariant());
    }

    [TestMethod]
    public void ToString_Format_X_UppercaseNoPrefix()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        string result = value.ToString("X");

        Assert.AreEqual(64, result.Length);
        Assert.IsFalse(result.StartsWith("0x"));
        Assert.AreEqual(result, result.ToUpperInvariant());
    }

    #endregion

    #region Property Tests

    [TestMethod]
    public void IsZero_Zero_ReturnsTrue()
    {
        Assert.IsTrue(Hash32.Zero.IsZero);
        Assert.IsTrue(default(Hash32).IsZero);
    }

    [TestMethod]
    public void IsZero_NonZero_ReturnsFalse()
    {
        Hash32 value = Hash32.Parse(TestHashHex);
        Assert.IsFalse(value.IsZero);
    }

    #endregion

    #region Conversion Tests

    [TestMethod]
    public void ExplicitConversion_FromByteArray_Works()
    {
        byte[] bytes = new byte[32];
        bytes[0] = 0x42;

        Hash32 hash = (Hash32)bytes;
        Assert.IsFalse(hash.IsZero);
    }

    [TestMethod]
    public void ToUInt256_RoundTrip()
    {
        Hash32 original = Hash32.Parse(TestHashHex);
        uint256 numeric = original.ToUInt256();
        Hash32 restored = Hash32.FromUInt256(numeric);

        Assert.AreEqual(original, restored);
    }

    #endregion

    #region Memory Layout Tests

    [TestMethod]
    public void StructSize_Is32Bytes()
    {
        Assert.AreEqual(32, System.Runtime.InteropServices.Marshal.SizeOf<Hash32>());
    }

    #endregion

    #region JSON Serialization Tests

    [TestMethod]
    public void JsonSerialize_CorrectFormat()
    {
        Hash32 hash = Hash32.Parse(TestHashHex);
        string json = System.Text.Json.JsonSerializer.Serialize(hash);

        Assert.AreEqual($"\"{TestHashHex}\"", json);
    }

    [TestMethod]
    public void JsonDeserialize_CorrectValue()
    {
        string json = $"\"{TestHashHex}\"";
        Hash32 hash = System.Text.Json.JsonSerializer.Deserialize<Hash32>(json);

        Assert.AreEqual(TestHashHex, hash.ToString());
    }

    [TestMethod]
    public void JsonRoundTrip_PreservesValue()
    {
        Hash32 original = Hash32.Parse(TestHashHex);
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        Hash32 restored = System.Text.Json.JsonSerializer.Deserialize<Hash32>(json);

        Assert.AreEqual(original, restored);
    }

    #endregion

    #region Blockchain Specific Tests

    [TestMethod]
    public void EVM_TransactionHash_Example()
    {
        // Example Ethereum transaction hash
        string txHash = "0xd4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3";
        Hash32 hash = Hash32.Parse(txHash);

        Assert.AreEqual(txHash, hash.ToString());
    }

    [TestMethod]
    public void Solana_Signature_LittleEndianRoundTrip()
    {
        // Solana uses little-endian encoding
        byte[] solanaBytes = new byte[32];
        for (int i = 0; i < 32; i++)
            solanaBytes[i] = (byte)(i + 1);

        Hash32 hash = Hash32.FromLittleEndian(solanaBytes);
        byte[] result = hash.ToLittleEndianBytes();

        CollectionAssert.AreEqual(solanaBytes, result);
    }

    #endregion
}
```

---

## Key Design Features

1. **Minimal Memory Footprint**: Uses exactly 32 bytes (4 × `ulong`) with `StructLayout(LayoutKind.Sequential)`

2. **High Performance**: 
   - `readonly struct` prevents defensive copying
   - `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot paths
   - Span-based parsing avoids allocations

3. **Blockchain Compatible**:
   - Big-endian support for EVM (Ethereum)
   - Little-endian support for Solana
   - Standard hex formatting with 0x prefix

4. **Fully Equatable & Comparable**:
   - Implements `IEquatable<Hash32>`, `IComparable<Hash32>`, `IComparable`
   - Works as dictionary keys
   - Sortable with lexicographic ordering

5. **JSON Serialization**: Built-in `JsonConverter` for API compatibility

You're in **ask mode**, so you'll need to copy these code blocks into your files or switch to **agent mode** if you'd like me to apply them directly.
