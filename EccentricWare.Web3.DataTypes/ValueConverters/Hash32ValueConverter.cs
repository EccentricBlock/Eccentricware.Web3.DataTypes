using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core value converter for <see cref="Hash32"/> to <see cref="byte[]"/> using big-endian encoding.
/// Intended for storage columns such as BINARY(32) / VARBINARY(32).
/// </summary>
public sealed class Hash32ValueConverter : ValueConverter<Hash32, byte[]>
{
    /// <summary>
    /// Creates a converter that serialises <see cref="Hash32"/> to a 32-byte big-endian array and back.
    /// </summary>
    public Hash32ValueConverter(ConverterMappingHints? mappingHints = null)
        : base(
            model => Hash32ToBytes(model),
            provider => BytesToHash32(provider),
            mappingHints)
    {
    }

    private static byte[] Hash32ToBytes(Hash32 value)
    {
        // Provider expects a byte[]; allocation is unavoidable for this boundary.
        // Big-endian is canonical for EVM hashes and sorts lexicographically.
        var bytes = new byte[Hash32.ByteLength];
        value.WriteBigEndian(bytes);
        return bytes;
    }

    private static Hash32 BytesToHash32(byte[] bytes)
    {
        if (bytes is null || bytes.Length != Hash32.ByteLength)
            throw new FormatException($"Expected {Hash32.ByteLength} bytes for {nameof(Hash32)}.");

        return new Hash32(bytes);
    }
}