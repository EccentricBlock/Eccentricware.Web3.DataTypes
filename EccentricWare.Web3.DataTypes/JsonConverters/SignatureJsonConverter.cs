using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for Signature that serializes as hex string with 0x prefix.
/// Automatically handles both EVM (130 chars) and Solana (128 chars) signatures.
/// </summary>
public sealed class SignatureJsonConverter : JsonConverter<Signature>
{
    public override Signature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert {reader.TokenType} to Signature");

        var str = reader.GetString();
        if (str is null)
            return Signature.Zero;

        return Signature.Parse(str, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, Signature value, JsonSerializerOptions options)
    {
        // Write directly to UTF-8 buffer without string allocation
        // Max size: "0x" + 130 hex chars (EVM)
        Span<byte> buffer = stackalloc byte[Signature.EvmHexLength + 2];
        if (value.TryFormat(buffer, out int bytesWritten, default, CultureInfo.InvariantCulture))
        {
            writer.WriteStringValue(buffer.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

