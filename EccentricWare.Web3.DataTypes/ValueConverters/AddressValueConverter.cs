using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core value converter for persisting <see cref="Address"/> into a single binary column.
/// Encoding: 1 byte <see cref="AddressType"/> + raw address bytes (20 for EVM, 32 for Solana).
/// </summary>
public sealed class AddressValueConverter : ValueConverter<Address, byte[]>
{
    /// <summary>Creates a converter that stores <see cref="Address"/> as a compact binary payload.</summary>
    public AddressValueConverter(ConverterMappingHints? mappingHints = null)
        : base(
            model => AddressToBytes(model),
            provider => BytesToAddress(provider),
            mappingHints)
    {
    }

    /// <summary>Encodes an <see cref="Address"/> into a compact binary representation.</summary>
    public static byte[] AddressToBytes(Address address)
    {
        int addressLength = address.ByteLength;
        byte[] payload = new byte[1 + addressLength];

        payload[0] = (byte)address.Type;
        address.WriteBytes(payload.AsSpan(1));

        return payload;
    }

    /// <summary>Decodes an <see cref="Address"/> from the compact binary representation.</summary>
    public static Address BytesToAddress(byte[] payload)
    {
        if (payload is null || payload.Length < 1)
            throw new ArgumentException("Address payload is null or empty.", nameof(payload));

        AddressType addressType = (AddressType)payload[0];
        ReadOnlySpan<byte> bytes = payload.AsSpan(1);

        return addressType switch
        {
            AddressType.Evm when bytes.Length == Address.EvmByteLength => Address.FromEvmBytes(bytes),
            AddressType.Solana when bytes.Length == Address.SolanaByteLength => Address.FromSolanaBytes(bytes),
            _ => throw new FormatException($"Invalid {nameof(Address)} payload length {bytes.Length} for type {addressType}.")
        };
    }
}