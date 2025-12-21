using EccentricWare.Web3.DataTypes.Utils.Uint256;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// System.Text.Json converter for <see cref="uint256"/>.
/// </summary>
/// <remarks>
/// Metadata tags: [hotpath] [json] [rpc] [canonicalisation]
///
/// Behaviour:
/// - Reads using <see cref="uint256.TryReadJsonRpcValue(ref Utf8JsonReader, UInt256ParseMode, out uint256)"/>.
/// - Writes as a JSON string using a configurable format (default: "0x" EVM quantity).
/// - Decimal write formats ("D"/"d") are supported but are slow-path (BigInteger).
/// </remarks>
public sealed class UInt256JsonConverter : JsonConverter<uint256>
{
    /// <summary>Parsing mode used on read.</summary>
    public UInt256ParseMode ParseMode { get; }

    /// <summary>
    /// Format string used on write (default: "0x").
    /// Supported: "", "x", "X", "0x", "0X", "x64", "X64", "0x64", "0X64", "D", "d".
    /// </summary>
    public string WriteFormat { get; }

    /// <summary>
    /// Creates a converter with strict EVM-quantity parsing and "0x" write format.
    /// </summary>
    public UInt256JsonConverter()
        : this(UInt256ParseMode.EvmQuantityStrict, writeFormat: "0x")
    {
    }

    /// <summary>
    /// Creates a converter with explicit read mode and write format.
    /// </summary>
    /// <param name="parseMode">Strict parsing mode.</param>
    /// <param name="writeFormat">Write format (see <see cref="WriteFormat"/>).</param>
    public UInt256JsonConverter(UInt256ParseMode parseMode, string writeFormat)
    {
        ParseMode = parseMode;
        WriteFormat = string.IsNullOrEmpty(writeFormat) ? "0x" : writeFormat;
    }

    /// <inheritdoc />
    public override uint256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (uint256.TryReadJsonRpcValue(ref reader, ParseMode, out uint256 value))
            return value;

        throw new JsonException($"Invalid {nameof(uint256)} value for parse mode {ParseMode} (token: {reader.TokenType}).");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, uint256 value, JsonSerializerOptions options)
    {
        // Always write as JSON string to preserve full 256-bit range.
        // Hex formats are hot-path; decimal formats are slow-path.
        if (WriteFormat.Length == 1 && (WriteFormat[0] == 'D' || WriteFormat[0] == 'd'))
        {
            // Slow-path decimal string (allocates).
            writer.WriteStringValue(value.ToBigInteger().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        // Worst case for hex formats is 66 bytes: "0x" + 64 hex digits.
        Span<byte> utf8 = stackalloc byte[66];

        if (!value.TryFormat(utf8, out int bytesWritten, WriteFormat.AsSpan(), provider: null))
            throw new JsonException($"Failed to format {nameof(uint256)} using format '{WriteFormat}'.");

        writer.WriteStringValue(utf8.Slice(0, bytesWritten));
    }
}
