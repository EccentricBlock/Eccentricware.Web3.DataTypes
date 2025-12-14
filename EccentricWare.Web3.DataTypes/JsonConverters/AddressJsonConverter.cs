using System.Text.Json;
using System.Text.Json.Serialization;

namespace EccentricWare.Web3.DataTypes.JsonConverters;

/// <summary>
/// JSON converter for Address that serializes as string.
/// EVM: hex with 0x prefix. Solana: Base58.
/// </summary>
public sealed class AddressJsonConverter : JsonConverter<Address>
{
    public override Address Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert {reader.TokenType} to Address");

        var str = reader.GetString();
        if (str is null)
            return Address.Zero;

        return Address.Parse(str);
    }

    public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
    {
        Span<byte> buffer = stackalloc byte[Address.MaxBase58Length + 2];
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            writer.WriteStringValue(buffer.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

