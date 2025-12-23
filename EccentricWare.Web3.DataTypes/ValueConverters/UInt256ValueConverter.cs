using Microsoft.EntityFrameworkCore.Storage.ValueConversion;



namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core provider converter for storing <see cref="uint256"/> as a fixed-length 32-byte array.
/// Big-endian encoding is used to preserve numeric ordering under lexicographic byte ordering (useful for certain DB indices).
/// </summary>
public sealed class UInt256BytesValueConverter : ValueConverter<uint256, byte[]>
{
    /// <summary>
    /// Creates a converter that maps uint256 &lt;-&gt; 32-byte big-endian array.
    /// </summary>
    public UInt256BytesValueConverter()
        : base(
            model => ConvertToProvider(model),
            provider => ConvertFromProvider(provider))
    {
    }

    /// <summary>
    /// Converts a uint256 to a 32-byte big-endian array.
    /// </summary>
    private new static byte[] ConvertToProvider(uint256 value) =>
        // Fixed-length binary representation is typically what you want for persistence/indexing.
        value.ToBigEndianBytes();

    /// <summary>
    /// Converts a 32-byte big-endian array to uint256.
    /// </summary>
    private new static uint256 ConvertFromProvider(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != 32)
            throw new FormatException("uint256 provider value must be exactly 32 bytes.");

        return new uint256(bytes);
    }
}